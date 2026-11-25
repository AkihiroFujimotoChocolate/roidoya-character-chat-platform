#if PHASE2_GATEWAY_TESTS
// SPDX-License-Identifier: MIT
using System.Collections.Concurrent;
using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using LineBot.Api.Processing;
using LineBot.Api.Queueing;
using LineBot.Api.Tests.ExternalContracts.V01.Line;
using LineBot.Api.Tests.ExternalContracts.V01.ConnectionManagement;
using LineBot.Api.Tests.ExternalContracts.V01.Support;
using LineBot.Api.Tests.InternalContracts.Support;
using LineBot.Api.Tests.InternalContracts.Support.V01;
using LineBot.Api.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit.Abstractions;

namespace LineBot.Api.Tests.InternalContracts.Queueing;

public sealed class Phase2WebhookQueueTestsC01(ITestOutputHelper output) : DiagnosticTest(output)
{
    [Theory(DisplayName = "C01-Internal-13: Verified intake filters and enqueues each event independently")]
    [Trait("Case", "C01-Internal-13")]
    [InlineData("C01-Internal-13-01", "Accepted items preserve unknown signed JSON", "accepted")]
    [InlineData("C01-Internal-13-02", "Invalid signature does not enqueue", "invalid")]
    [InlineData("C01-Internal-13-03", "Standby is filtered apart from target fixed reply", "standby")]
    [InlineData("C01-Internal-13-04", "A failed fixed reply does not prevent the next attempt", "fails-first")]
    public async Task Webhook_VerificationAndQueueHandoff_PreserveEventShape(string caseId, string description, string condition)
    {
        const string fixedText = "  満杯です  ";
        var first = ExternalSamples.Random();
        var second = ExternalSamples.Random() with { ConnectionId = first.ConnectionId,
            Destination = first.Destination, CharacterId = first.CharacterId };
        var bytes = LineWebhookSamples.WebhookWithEvents(first.Destination, first.Body(),
            second.Body(mode: condition == "standby" ? "standby" : "active"));
        var root = JsonNode.Parse(bytes)!.AsObject();
        root["synthetic_root"] = JsonNode.Parse("{\"number\":20,\"flag\":true}");
        var events = root["events"]!.AsArray();
        events[0]!["synthetic_event"] = JsonNode.Parse("[true,null,10]");
        events[1]!["synthetic_event"] = JsonNode.Parse("{\"number\":21}");
        bytes = JsonSerializer.SerializeToUtf8Bytes(root);
        var signature = LineWebhookSamples.Sign(bytes, condition == "invalid" ? "alternate-test-secret" : LineWebhookSamples.Secret);
        var calls = new ConcurrentQueue<(string Event, string Operation)>();
        var filtered = new ConcurrentQueue<(string Event, QueueItem Item, bool Target)>();
        var isFull = condition is "standby" or "fails-first";
        var queue = new Phase2QueueDouble { Write = item =>
        {
            calls.Enqueue((Phase2FlowSamples.EventId(item), "enqueue"));
            return isFull ? QueueWriteResult.Full : QueueWriteResult.Accepted;
        } };
        var reply = new ReplyBoundaryDouble { Respond = (context, _) =>
        {
            calls.Enqueue((context.WebhookEventId, "reply"));
            return Task.FromResult(condition != "fails-first" || context.WebhookEventId != first.EventId);
        } };
        using var outbound = new CaptureHandler((_, _) => throw new InvalidOperationException("Unexpected Chat/LINE request."));
        using var connections = ConnectionSamples.Capture(first);
        using var host = new GatewayApplicationFactory(outbound, outbound, connections, services =>
        {
            services.RemoveAll<ILineWebhookEventQueue>();
            services.AddSingleton<ILineWebhookEventQueue>(queue);
            if (isFull)
            {
                services.RemoveAll<ILineReplyService>();
                services.AddScoped<ILineReplyService>(_ => reply);
            }
            if (condition is "accepted" or "standby")
            {
                var production = Assert.Single(services.Where(service => service.ServiceType == typeof(ILineEventFilter)));
                services.Remove(production);
                services.AddScoped<ILineEventFilter>(provider => new RecordingFilter(
                    (ILineEventFilter)(production.ImplementationFactory?.Invoke(provider) ??
                        ActivatorUtilities.CreateInstance(provider, production.ImplementationType!)), calls, filtered));
            }
        });
        if (isFull) host.Settings["Reply:QueueFullMessages:0"] = fixedText;
        Trace(caseId, new { first, second, description, condition, host.Settings, BodyBase64 = Convert.ToBase64String(bytes), signature,
            Connection = ConnectionSamples.Connection(first), ExpectedStatus = condition == "invalid" ? 400 : 200, Fixed = fixedText, WaitSeconds = 10 });
        using var client = host.CreateClient();
        using var request = LineWebhookSamples.Incoming(bytes, signature, "/line/webhook");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            using var response = await client.SendAsync(request, deadline.Token);
            Assert.Equal(condition == "invalid" ? HttpStatusCode.BadRequest : HttpStatusCode.OK, response.StatusCode);
            await host.StopHostAsync();
            Assert.Empty(outbound.Requests);
            if (condition == "invalid")
            {
                var type = Assert.Single(typeof(global::Program).Assembly.GetTypes().Where(type => type.Name == "VerifiedLineWebhook"));
                var constructors = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                Assert.NotEmpty(constructors);
                Assert.All(constructors, constructor => Assert.True(constructor.IsPrivate));
                Assert.Empty(queue.Writes);
            }
            else if (condition == "accepted")
            {
                var items = queue.Writes.ToArray();
                Assert.Equal(2, items.Length);
                for (var i = 0; i < items.Length; i++)
                {
                    var expected = (JsonObject)root.DeepClone();
                    expected.Remove("events");
                    expected["event"] = events[i]!.DeepClone();
                    var item = items[i];
                    var eventId = Phase2FlowSamples.EventId(item);
                    Assert.Null(item.ConnectionId);
                    Assert.True(JsonNode.DeepEquals(expected, JsonNode.Parse(item.Webhook.GetRawText())));
                    var check = Assert.Single(filtered, call => call.Event == eventId);
                    Assert.True(check.Target);
                    Assert.Equal(item, check.Item);
                    Assert.Equal(new[] { "filter", "enqueue" }, calls.Where(call => call.Event == eventId).Select(call => call.Operation));
                }
                Assert.Equal(new[] { first.EventId, second.EventId }, items.Select(Phase2FlowSamples.EventId));
            }
            else
            {
                var targets = condition == "standby" ? new[] { first } : new[] { first, second };
                Assert.Equal(targets.Select(value => value.EventId), queue.Writes.Select(Phase2FlowSamples.EventId));
                Assert.Equal(targets.Length, reply.Calls.Count);
                foreach (var value in targets)
                {
                    var call = Assert.Single(reply.Calls, entry => entry.Context.WebhookEventId == value.EventId);
                    Assert.Equal(new[] { fixedText }, call.Messages);
                    Assert.IsType<LineReplyToken>(call.Context.ReplyToken);
                    Assert.IsType<ChannelAccessToken>(call.Context.ChannelAccessToken);
                    await Phase2ChatInputsC01.AssertReplyTokensAsync(call.Context, value.ReplyToken);
                }
                if (condition == "standby")
                {
                    Assert.Equal(new[] { "filter", "enqueue", "reply" }, calls.Where(call => call.Event == first.EventId).Select(call => call.Operation));
                    Assert.Equal("filter", Assert.Single(calls, call => call.Event == second.EventId).Operation);
                    Assert.True(Assert.Single(filtered, call => call.Event == first.EventId).Target);
                    Assert.False(Assert.Single(filtered, call => call.Event == second.EventId).Target);
                    Assert.Equal(Assert.Single(queue.Writes), Assert.Single(filtered, call => call.Event == first.EventId).Item);
                }
            }
            Assert.Empty(host.Unexpected.Requests);
            Trace(caseId + "-observed", new
            {
                Calls = calls.Select(call => new { call.Event, call.Operation }).ToArray(),
                Items = queue.Writes.ToArray(),
                Reply = reply.Calls.Select(call => new { call.Context, call.Messages, CanBeCanceled = call.Token.CanBeCanceled, IsCancelled = call.Token.IsCancellationRequested }).ToArray()
            });
        }
        finally { await host.StopHostAsync(); }
    }

    private sealed class RecordingFilter(ILineEventFilter production, ConcurrentQueue<(string Event, string Operation)> calls,
        ConcurrentQueue<(string Event, QueueItem Item, bool Target)> filtered) : ILineEventFilter
    {
        public async Task<bool> IsTargetAsync(QueueItem item, CancellationToken cancellationToken = default)
        {
            var id = Phase2FlowSamples.EventId(item);
            var target = await production.IsTargetAsync(item, cancellationToken);
            filtered.Enqueue((id, item, target));
            calls.Enqueue((id, "filter"));
            return target;
        }
    }
}
#endif
