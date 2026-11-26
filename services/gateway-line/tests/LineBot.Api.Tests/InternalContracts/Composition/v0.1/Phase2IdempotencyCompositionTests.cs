#if PHASE2_GATEWAY_TESTS
// SPDX-License-Identifier: MIT
using System.Collections.Concurrent;
using System.Net;
using LineBot.Api.Chat;
using LineBot.Api.Processing;
using LineBot.Api.Tests.ExternalContracts.V01.Line;
using LineBot.Api.Tests.ExternalContracts.V01.ConnectionManagement;
using LineBot.Api.Tests.ExternalContracts.V01.Support;
using LineBot.Api.Tests.InternalContracts.Support;
using LineBot.Api.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit.Abstractions;
using IIdempotencyService = LineBot.Api.Services.IIdempotencyService;
using IdempotencyService = LineBot.Api.Services.IdempotencyService;
using Request = LineBot.Api.Contracts.V0_1.ChatServiceRequest;

namespace LineBot.Api.Tests.InternalContracts.Composition.V01;

public sealed class Phase2IdempotencyCompositionTests(ITestOutputHelper output) : DiagnosticTest(output)
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(10);

    [Fact(DisplayName = "C01-Internal-17-12: Production idempotency registration is Singleton and replaceable")]
    [Trait("Case", "C01-Internal-17-12")]
    public async Task Program_IdempotencyRegistration_IsSingletonAndReplaceable()
    {
        const string caseId = "C01-Internal-17-12";
        var data = ExternalSamples.Random();
        using var outbound = new CaptureHandler((_, _) =>
            throw new InvalidOperationException("Chat/LINE HTTP was not expected."));
        using var connections = ConnectionSamples.Capture(data);
        using (var original = new GatewayApplicationFactory(outbound, outbound, connections))
        {
            Trace(caseId + "-original", new { original.Settings, Registration = typeof(IIdempotencyService).FullName,
                ExpectedType = typeof(IdempotencyService).FullName, ExpectedSameInstance = true, WaitSeconds = 10 });
            using var client = original.CreateClient();
            try
            {
                using var first = original.Services.CreateScope();
                using var second = original.Services.CreateScope();
                var instance = original.Services.GetRequiredService<IIdempotencyService>();
                Assert.IsType<IdempotencyService>(instance);
                Assert.Same(instance, first.ServiceProvider.GetRequiredService<IIdempotencyService>());
                Assert.Same(instance, second.ServiceProvider.GetRequiredService<IIdempotencyService>());
            }
            finally { await original.StopHostAsync(); }
        }

        var redelivery = data with { EventId = GeneratedValues.Id("webhook-event") };
        var normalRegistered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var redeliveryRegistered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var order = new ConcurrentQueue<(string Operation, string EventId)>();
        var record = new IdempotencyDouble
        {
            Check = id => { order.Enqueue(("check", id)); return false; },
            Registered = id =>
            {
                order.Enqueue(("mark", id));
                if (id == data.EventId) normalRegistered.TrySetResult();
                if (id == redelivery.EventId) redeliveryRegistered.TrySetResult();
            }
        };
        var chat = new ChatBoundaryDouble<Request>((_, _) => Task.FromResult(
            new ChatServiceResult { Status = "ok", Messages = [data.ReplyText] }));
        var reply = new ReplyBoundaryDouble
        {
            Respond = (context, _) =>
            {
                order.Enqueue(("reply-success", context.WebhookEventId));
                return Task.FromResult(true);
            }
        };
        using var host = new GatewayApplicationFactory(outbound, outbound, connections, services =>
        {
            services.RemoveAll<IIdempotencyService>();
            services.AddSingleton<IIdempotencyService>(record);
            services.RemoveAllKeyed<IChatService<Request>>("0.1");
            services.AddKeyedScoped<IChatService<Request>>("0.1", (_, _) => chat);
            services.RemoveAll<ILineReplyService>();
            services.AddScoped<ILineReplyService>(_ => reply);
        });
        var expected = new (string Operation, string EventId)[]
        {
            ("reply-success", data.EventId), ("mark", data.EventId),
            ("check", redelivery.EventId), ("reply-success", redelivery.EventId), ("mark", redelivery.EventId)
        };
        var regularBody = data.Body();
        var redeliveryBody = redelivery.Body(redelivery: true);
        var requests = new[]
        {
            (Id: data.EventId, Body: regularBody, Signature: LineWebhookSamples.Sign(regularBody), Registered: normalRegistered.Task),
            (Id: redelivery.EventId, Body: redeliveryBody, Signature: LineWebhookSamples.Sign(redeliveryBody), Registered: redeliveryRegistered.Task)
        };
        var path = "/line/webhook";
        Trace(caseId, new
        {
            data, host.Settings, path, WaitSeconds = 10, LookupReturnsProcessed = false,
            Requests = requests.Select(r => new { r.Id, BodyBase64 = Convert.ToBase64String(r.Body), r.Signature }),
            Connection = ConnectionSamples.Connection(data),
            ConnectionResponse = "200 application/json", Reply = true, ChatStatus = "ok", Message = data.ReplyText,
            Registration = typeof(IIdempotencyService).FullName, ExpectedSameInstance = true,
            ExpectedOrder = expected.Select(c => new { c.Operation, c.EventId }), ExpectedChat = 2, ExpectedReply = 2, ExpectedOutbound = 0
        });
        using var http = host.CreateClient();
        try
        {
            using var first = host.Services.CreateScope();
            using var second = host.Services.CreateScope();
            Assert.Same(record, host.Services.GetRequiredService<IIdempotencyService>());
            Assert.Same(record, first.ServiceProvider.GetRequiredService<IIdempotencyService>());
            Assert.Same(record, second.ServiceProvider.GetRequiredService<IIdempotencyService>());
            Assert.Same(chat, first.ServiceProvider.GetRequiredKeyedService<IChatService<Request>>("0.1"));
            Assert.Same(reply, first.ServiceProvider.GetRequiredService<ILineReplyService>());

            foreach (var item in requests)
            {
                using var request = LineWebhookSamples.Incoming(item.Body, item.Signature, path);
                using var deadline = new CancellationTokenSource(Limit);
                using var response = await http.SendAsync(request, deadline.Token);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                await item.Registered.WaitAsync(Limit);
            }
            await host.StopHostAsync();
            Trace(caseId + "-observed", new { Calls = order.Select(c => new { c.Operation, c.EventId }),
                ChatCount = chat.Calls.Count, ReplyCount = reply.Calls.Count, OutboundCount = outbound.Requests.Count });
            Assert.Equal(expected, order.ToArray());
            Assert.Equal(2, chat.Calls.Count);
            Assert.Equal(2, reply.Calls.Count);
            Assert.Empty(outbound.Requests);
        }
        finally { await host.StopHostAsync(); }
    }
}
#endif
