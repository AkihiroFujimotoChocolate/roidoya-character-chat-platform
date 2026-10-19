// SPDX-License-Identifier: MIT
using System.Net;
using LineBot.Api.Tests.Support;
using LineBot.Api.Tests.ExternalContracts.V01.Support;
using LineBot.Api.Tests.ExternalContracts.V01.Line;
using Xunit.Abstractions;
namespace LineBot.Api.Tests.ExternalContracts.V01.Chat;

public sealed class ChatHttpResponseContractTests : ExternalFlowTest
{
    public ChatHttpResponseContractTests(ITestOutputHelper output) : base(output) { }

    [Theory(DisplayName = "External-15: A normal Chat response produces the matching LINE reply")]
    [Trait("Case", "External-15")]
    [InlineData("External-15-01", "An immediate Chat response produces the matching LINE reply", false)]
    [InlineData("External-15-02", "A gated Chat response produces no LINE reply before release and the matching reply after release", true)]
    public async Task Webhook_NormalChatResponse_SendsLineReply(string caseId, string caseDescription, bool delayed)
    {
        // Arrange
        var data = ExternalSamples.Random();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var chat = new CaptureHandler(async (_, token) => {
            if (delayed) await release.Task.WaitAsync(token);
            return ChatV01Samples.Ok(data.ReplyText);
        });
        using var line = new CaptureHandler((_, _) => Task.FromResult(LineReplySamples.SuccessResponse()));
        using var host = new GatewayApplicationFactory(chat, line);
        // The gate orders the test. Elapsed milliseconds are not an assertion.
        host.Settings["Chat:RequestTimeoutSeconds"] = "5";
        var body = data.Body();
        Record(caseId, host, data, body, LineWebhookSamples.Sign(body),
            new { Delayed = delayed, ChatStatus = 200, Messages = new[] { data.ReplyText }, LineStatus = 200 });
        using var client = host.CreateClient();
        try {
            using var request = LineWebhookSamples.Incoming(body, LineWebhookSamples.Sign(body));
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            // Act
            using var webhookResponse = await client.SendAsync(request, deadline.Token);

            // Assert
            Assert.Equal(HttpStatusCode.OK, webhookResponse.StatusCode);
            deadline.Dispose();
            ChatV01Assertions.AssertRequest(await host.Chat.NextAsync(), data, "https://chat.test/chat/v0.1/generate-replies");
            if (delayed) Assert.Empty(host.Line.Requests);
            release.TrySetResult();
            LineReplyAssertions.AssertRequest(await host.Line.NextAsync(), data, data.ReplyText);
            await Task.WhenAll(chat.AssertNoPendingRequestsAsync(), line.AssertNoPendingRequestsAsync());
            await host.StopHostAsync();
            Assert.Single(chat.Requests);
            Assert.Single(line.Requests);
        }
        finally { release.TrySetResult(); }
    }

    [Fact(DisplayName = "External-18: Provider failure uses the configured default fallback")]
    [Trait("Case", "External-18")]
    public async Task Webhook_ProviderError_SendsDefaultFallback()
    {
        // Arrange
        var data = ExternalSamples.Random();
        var fallback = GeneratedValues.Id("fallback");
        const string response = "{\"request_id\":\"correlation-test\",\"status\":\"provider_error\",\"messages\":[],\"fallback_used\":false,\"error\":{\"code\":\"runtime_error\",\"message\":\"diagnostic-only\"}}";
        using var chat = new CaptureHandler((_, _) => Task.FromResult(HttpResponses.Json(response)));
        using var line = new CaptureHandler((_, _) => Task.FromResult(LineReplySamples.SuccessResponse()));
        using var host = new GatewayApplicationFactory(chat, line);
        host.Settings["Chat:Fallbacks:Default"] = fallback;
        var body = data.Body();
        Record("External-18-01", host, data, body, LineWebhookSamples.Sign(body),
            new { ChatStatus = 200, Response = response, ExpectedReply = fallback, LineStatus = 200 });
        using var client = host.CreateClient();
        using var request = LineWebhookSamples.Incoming(body, LineWebhookSamples.Sign(body));
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // Act
        using var webhookResponse = await client.SendAsync(request, deadline.Token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, webhookResponse.StatusCode);
        deadline.Dispose();
        ChatV01Assertions.AssertRequest(await host.Chat.NextAsync(), data, "https://chat.test/chat/v0.1/generate-replies");
        LineReplyAssertions.AssertRequest(await host.Line.NextAsync(), data, fallback);
        await Task.WhenAll(chat.AssertNoPendingRequestsAsync(), line.AssertNoPendingRequestsAsync());
        await host.StopHostAsync();
        Assert.Single(chat.Requests);
        Assert.Single(line.Requests);
    }

    [Theory(DisplayName = "External-21: Chat HTTP failure uses the default fallback")]
    [Trait("Case", "External-21")]
    [InlineData("External-21-01", "HTTP 400 with provider_error produces the default fallback", "400", 400)]
    [InlineData("External-21-02", "HTTP 500 produces the default fallback", "500", 500)]
    [InlineData("External-21-03", "A transport exception produces the default fallback", "transport", 0)]
    public async Task Webhook_ChatHttpFailure_SendsDefaultFallback(string caseId, string caseDescription, string failure, int statusCode)
    {
        // Arrange
        var data = ExternalSamples.Random();
        const string response = "{\"status\":\"provider_error\",\"messages\":[],\"error\":{\"code\":\"fail_test\",\"message\":\"diagnostic-only\"}}";
        using var chat = new CaptureHandler((_, _) => failure == "transport"
            ? Task.FromException<HttpResponseMessage>(new HttpRequestException("synthetic transport failure"))
            : Task.FromResult(HttpResponses.Json(response, (HttpStatusCode)statusCode)));
        using var line = new CaptureHandler((_, _) => Task.FromResult(LineReplySamples.SuccessResponse()));
        using var host = new GatewayApplicationFactory(chat, line);
        var body = data.Body();
        Record(caseId,
            host, data, body, LineWebhookSamples.Sign(body),
            new { Failure = failure, Response = response, ExpectedReply = ChatV01Samples.DefaultFallback, LineStatus = 200 });
        using var client = host.CreateClient();
        using var request = LineWebhookSamples.Incoming(body, LineWebhookSamples.Sign(body));
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // Act
        using var webhookResponse = await client.SendAsync(request, deadline.Token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, webhookResponse.StatusCode);
        deadline.Dispose();
        ChatV01Assertions.AssertRequest(await host.Chat.NextAsync(), data, "https://chat.test/chat/v0.1/generate-replies");
        LineReplyAssertions.AssertRequest(await host.Line.NextAsync(), data, ChatV01Samples.DefaultFallback);
        await Task.WhenAll(chat.AssertNoPendingRequestsAsync(), line.AssertNoPendingRequestsAsync());
        await host.StopHostAsync();
        Assert.Single(chat.Requests);
        Assert.Single(line.Requests);
    }

    [Fact(DisplayName = "External-20: Timeout ignores a late reply and the next event recovers")]
    [Trait("Case", "External-20")]
    public async Task Webhook_ChatTimeout_IgnoresLateReplyAndProcessesNextEvent()
    {
        // Arrange
        var first = ExternalSamples.Random();
        var next = ExternalSamples.Random();
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var upstream = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        using var chat = new CaptureHandler(async (_, token) => {
            if (Interlocked.Increment(ref calls) != 1) return ChatV01Samples.Ok(next.ReplyText);
            try { return await upstream.Task.WaitAsync(token); }
            finally { if (token.IsCancellationRequested) cancelled.TrySetResult(); }
        });
        using var line = new CaptureHandler((_, _) => Task.FromResult(LineReplySamples.SuccessResponse()));
        using var host = new GatewayApplicationFactory(chat, line);
        var firstBody = first.Body();
        var nextBody = next.Body();
        Record("External-20-01", host, first, firstBody, LineWebhookSamples.Sign(firstBody),
            new { ExpectedReply = ChatV01Samples.TimeoutFallback, LateReply = first.ReplyText,
                NextValues = next, NextBodyBase64 = Convert.ToBase64String(nextBody),
                NextSignature = LineWebhookSamples.Sign(nextBody), NextReply = next.ReplyText, LineStatus = 200 });
        using var client = host.CreateClient();
        try {
            using var request = LineWebhookSamples.Incoming(firstBody, LineWebhookSamples.Sign(firstBody));
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            // Act
            using var webhookResponse = await client.SendAsync(request, deadline.Token);

            // Assert
            Assert.Equal(HttpStatusCode.OK, webhookResponse.StatusCode);
            deadline.Dispose();
            ChatV01Assertions.AssertRequest(await host.Chat.NextAsync(), first, "https://chat.test/chat/v0.1/generate-replies");
            await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(10));
            LineReplyAssertions.AssertRequest(await host.Line.NextAsync(), first, ChatV01Samples.TimeoutFallback);
            using var lateResponse = ChatV01Samples.Ok(first.ReplyText);
            upstream.TrySetResult(lateResponse);
            using var nextRequest = LineWebhookSamples.Incoming(nextBody, LineWebhookSamples.Sign(nextBody));
            using var nextDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            // Act
            using var nextResponse = await client.SendAsync(nextRequest, nextDeadline.Token);

            // Assert
            Assert.Equal(HttpStatusCode.OK, nextResponse.StatusCode);
            nextDeadline.Dispose();
            ChatV01Assertions.AssertRequest(await host.Chat.NextAsync(), next, "https://chat.test/chat/v0.1/generate-replies");
            LineReplyAssertions.AssertRequest(await host.Line.NextAsync(), next, next.ReplyText);
            await Task.WhenAll(chat.AssertNoPendingRequestsAsync(), line.AssertNoPendingRequestsAsync());
            await host.StopHostAsync();
            Assert.Equal(2, chat.Requests.Count);
            Assert.Equal(2, line.Requests.Count);
        }
        finally { upstream.TrySetCanceled(); }
    }
}

