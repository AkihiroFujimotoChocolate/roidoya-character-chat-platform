#if PHASE2_GATEWAY_TESTS
// SPDX-License-Identifier: MIT
using LineBot.Api.Tests.InternalContracts.Support.V02;
using System.Net;
using LineBot.Api.Chat;
using LineBot.Api.Processing;
using LineBot.Api.Tests.ExternalContracts.V01.Line;
using LineBot.Api.Tests.ExternalContracts.V02.ConnectionManagement;
using LineBot.Api.Tests.ExternalContracts.V02.Support;
using LineBot.Api.Tests.InternalContracts.Support;
using LineBot.Api.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit.Abstractions;
using Request = LineBot.Api.Contracts.V0_2.ChatServiceRequest;

namespace LineBot.Api.Tests.InternalContracts.Composition.V02;

public sealed class Phase2HostedGatewayTests(ITestOutputHelper output) : DiagnosticTest(output)
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(10);

    [Fact(DisplayName = "C02-Internal-07-01: Production path uses substituted Chat and LINE reply boundaries")]
    [Trait("Case", "C02-Internal-07")]
    public async Task Program_AbstractOverrides_AreUsedByWorker()
    {
        var data = ExternalSamples.Random();
        var delivered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var chat = new ChatBoundaryDouble<Request>((_, _) =>
            Task.FromResult(new ChatServiceResult { Status = "ok", Messages = [data.ReplyText] }));
        var reply = new ReplyBoundaryDouble { Respond = (_, _) =>
        {
            delivered.TrySetResult();
            return Task.FromResult(true);
        } };
        using var outbound = new CaptureHandler((_, _) => throw new InvalidOperationException("Chat/LINE HTTP was not expected."));
        using var connections = ConnectionSamples.Capture(data);
        using var host = new GatewayApplicationFactory(outbound, outbound, connections, services =>
        {
            services.RemoveAllKeyed<IChatService<Request>>("0.2");
            services.AddKeyedScoped<IChatService<Request>>("0.2", (_, _) => chat);
            services.RemoveAll<ILineReplyService>();
            services.AddScoped<ILineReplyService>(_ => reply);
        });
        var bytes = data.Body();
        var signature = LineWebhookSamples.Sign(bytes);
        Trace("C02-Internal-07-01", new { data, host.Settings, BodyBase64 = Convert.ToBase64String(bytes), signature,
            Connection = ConnectionSamples.Connection(data), WaitSeconds = 10, ExpectedChat = 1, ExpectedReply = 1 });
        using var client = host.CreateClient();
        using var serviceScope = host.Services.CreateScope();
        Assert.Same(chat, serviceScope.ServiceProvider.GetRequiredKeyedService<IChatService<Request>>("0.2"));
        Assert.Same(reply, serviceScope.ServiceProvider.GetRequiredService<ILineReplyService>());
        using var request = LineWebhookSamples.Incoming(bytes, signature, "/line/webhook/" + data.ConnectionId);
        using var deadline = new CancellationTokenSource(Limit);
        try
        {
            using var response = await client.SendAsync(request, deadline.Token);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            await delivered.Task.WaitAsync(Limit);
            await host.StopHostAsync();
            Assert.Equal(data.Text, Assert.Single(chat.Calls).Request.MessageText);
            var call = Assert.Single(reply.Calls);
            Assert.Equal(new[] { data.ReplyText }, call.Messages);
            Assert.Equal(data.EventId, call.Context.WebhookEventId);
            await Phase2ChatInputs.AssertReplyTokensAsync(call.Context, data.ReplyToken);
            ConnectionSamples.AssertRequests(host, data, new[] { data });
            Assert.Empty(outbound.Requests);
        }
        finally { await host.StopHostAsync(); }
    }

    [Theory(DisplayName = "C02-Internal-12: Stopping the production host cancels an in-flight boundary")]
    [Trait("Case", "C02-Internal-12")]
    [InlineData("C02-Internal-12-01", "Stop during the Chat boundary", true)]
    [InlineData("C02-Internal-12-02", "Stop during the LINE reply boundary", false)]
    public async Task Host_Stop_CancelsActiveOperation(string caseId, string description, bool cancelChat)
    {
        var data = ExternalSamples.Random();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseBoundary = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var scopeDisposed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task Hold(CancellationToken token)
        {
            using var registration = token.Register(() => cancelled.TrySetResult());
            started.TrySetResult();
            try { await Task.Delay(Timeout.InfiniteTimeSpan, token); }
            finally { await releaseBoundary.Task.WaitAsync(Limit); }
        }
        var chat = new ChatBoundaryDouble<Request>(async (_, token) =>
        {
            if (cancelChat) await Hold(token);
            return new ChatServiceResult { Status = "ok", Messages = [data.ReplyText] };
        }, () => scopeDisposed.TrySetResult());
        var reply = new ReplyBoundaryDouble { Respond = async (_, token) =>
        {
            await Hold(token);
            return true;
        } };
        using var outbound = new CaptureHandler((_, _) => throw new InvalidOperationException("Chat/LINE HTTP was not expected."));
        using var connections = ConnectionSamples.Capture(data);
        using var host = new GatewayApplicationFactory(outbound, outbound, connections, services =>
        {
            services.RemoveAllKeyed<IChatService<Request>>("0.2");
            services.AddKeyedScoped<IChatService<Request>>("0.2", (_, _) => chat);
            services.RemoveAll<ILineReplyService>();
            services.AddScoped<ILineReplyService>(_ => reply);
        });
        host.Settings["Chat:RequestTimeoutSeconds"] = "60";
        var bytes = data.Body();
        var signature = LineWebhookSamples.Sign(bytes);
        Trace(caseId, new { data, description, host.Settings, BodyBase64 = Convert.ToBase64String(bytes), signature,
            Connection = ConnectionSamples.Connection(data), WaitSeconds = 10, ExpectedChat = 1, ExpectedReply = cancelChat ? 0 : 1 });
        using var client = host.CreateClient();
        using var request = LineWebhookSamples.Incoming(bytes, signature, "/line/webhook/" + data.ConnectionId);
        using var deadline = new CancellationTokenSource(Limit);
        try
        {
            using var response = await client.SendAsync(request, deadline.Token);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            await started.Task.WaitAsync(Limit);
            var stopping = host.StopHostAsync();
            await cancelled.Task.WaitAsync(Limit);
            Assert.False(stopping.IsCompleted);
            releaseBoundary.TrySetResult();
            await stopping.WaitAsync(Limit);
            Assert.True(scopeDisposed.Task.IsCompletedSuccessfully);
            Trace(caseId + "-observed", new
            {
                BoundaryCancelled = cancelled.Task.IsCompletedSuccessfully,
                ProcessingScopeDisposed = scopeDisposed.Task.IsCompletedSuccessfully
            });
            var call = Assert.Single(chat.Calls);
            if (cancelChat)
            {
                Assert.True(call.Token.IsCancellationRequested);
                Assert.Empty(reply.Calls);
            }
            else Assert.True(Assert.Single(reply.Calls).Token.IsCancellationRequested);
            ConnectionSamples.AssertRequests(host, data, new[] { data });
            Assert.Empty(outbound.Requests);
        }
        finally
        {
            releaseBoundary.TrySetResult();
            await host.StopHostAsync();
        }
    }
}
#endif
