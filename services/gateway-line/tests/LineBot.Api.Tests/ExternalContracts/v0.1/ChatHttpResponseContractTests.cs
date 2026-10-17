// SPDX-License-Identifier: MIT
using System.Net;
using LineBot.Api.Tests.Support;
using Xunit.Abstractions;
namespace LineBot.Api.Tests.ExternalContracts.V01;

public sealed class ChatHttpResponseContractTests : ExternalFlowTest
{
    public ChatHttpResponseContractTests(ITestOutputHelper output) : base(output) { }

    [Theory, Trait("Case", "External-15")]
    [InlineData(false)] // External-15-01
    [InlineData(true)]  // External-15-02
    public async Task A_normal_chat_reply_reaches_the_line_http_boundary(bool delayed)
    {
        var data = ExternalSamples.Random();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var chat = new CaptureHandler(async (_, token) => {
            if (delayed) await release.Task.WaitAsync(token);
            return Samples.Ok(data.ReplyText);
        });
        using var host = new ExternalGatewayFactory(chat);
        // The gate orders the test. Elapsed milliseconds are not an assertion.
        host.Settings["Chat:RequestTimeoutSeconds"] = "5";
        var body = data.Body();
        Record(delayed ? "External-15-02" : "External-15-01", host, data, body, Samples.Sign(body),
            new { Delayed = delayed, ChatStatus = 200, Messages = new[] { data.ReplyText }, LineStatus = 200 });
        using var client = host.CreateClient();
        try {
            await SendAsync(client, body, Samples.Sign(body));
            ExternalAssertions.Chat(await host.Chat.NextAsync(), data, "https://chat.test/chat/v0.1/generate-replies");
            if (delayed) Assert.Empty(host.Line.Requests);
            release.TrySetResult();
            ExternalAssertions.Line(await host.Line.NextAsync(), data, data.ReplyText);
            await FinishAsync(host, 1, 1);
        }
        finally { release.TrySetResult(); }
    }

    [Fact, Trait("Case", "External-18")]
    public async Task A_provider_error_becomes_the_configured_fallback()
    {
        var data = ExternalSamples.Random();
        var fallback = GeneratedValues.Id("fallback");
        const string response = "{\"request_id\":\"correlation-test\",\"status\":\"provider_error\",\"messages\":[],\"fallback_used\":false,\"error\":{\"code\":\"runtime_error\",\"message\":\"diagnostic-only\"}}";
        using var chat = new CaptureHandler((_, _) => Task.FromResult(Samples.Json(response)));
        using var host = new ExternalGatewayFactory(chat);
        host.Settings["Chat:Fallbacks:Default"] = fallback;
        var body = data.Body();
        Record("External-18-01", host, data, body, Samples.Sign(body),
            new { ChatStatus = 200, Response = response, ExpectedReply = fallback, LineStatus = 200 });
        using var client = host.CreateClient();
        await SendAsync(client, body, Samples.Sign(body));
        ExternalAssertions.Chat(await host.Chat.NextAsync(), data, "https://chat.test/chat/v0.1/generate-replies");
        ExternalAssertions.Line(await host.Line.NextAsync(), data, fallback);
        await FinishAsync(host, 1, 1);
    }

    [Theory, Trait("Case", "External-21")]
    [InlineData("400")] // External-21-01
    [InlineData("500")] // External-21-02
    [InlineData("transport")] // External-21-03
    public async Task Chat_http_failure_becomes_the_default_fallback(string failure)
    {
        var data = ExternalSamples.Random();
        const string response = "{\"status\":\"provider_error\",\"messages\":[],\"error\":{\"code\":\"fail_test\",\"message\":\"diagnostic-only\"}}";
        using var chat = new CaptureHandler((_, _) => failure == "transport"
            ? Task.FromException<HttpResponseMessage>(new HttpRequestException("synthetic transport failure"))
            : Task.FromResult(Samples.Json(response, failure == "400" ? HttpStatusCode.BadRequest : HttpStatusCode.InternalServerError)));
        using var host = new ExternalGatewayFactory(chat);
        var body = data.Body();
        Record("External-21-0" + (failure == "400" ? "1" : failure == "500" ? "2" : "3"),
            host, data, body, Samples.Sign(body),
            new { Failure = failure, Response = response, ExpectedReply = Samples.DefaultFallback, LineStatus = 200 });
        using var client = host.CreateClient();
        await SendAsync(client, body, Samples.Sign(body));
        ExternalAssertions.Chat(await host.Chat.NextAsync(), data, "https://chat.test/chat/v0.1/generate-replies");
        ExternalAssertions.Line(await host.Line.NextAsync(), data, Samples.DefaultFallback);
        await FinishAsync(host, 1, 1);
    }

    [Fact, Trait("Case", "External-20")]
    public async Task Timeout_does_not_send_a_late_reply_and_the_next_event_recovers()
    {
        var first = ExternalSamples.Random();
        var next = ExternalSamples.Random();
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var upstream = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        using var chat = new CaptureHandler(async (_, token) => {
            if (Interlocked.Increment(ref calls) != 1) return Samples.Ok(next.ReplyText);
            try { return await upstream.Task.WaitAsync(token); }
            finally { if (token.IsCancellationRequested) cancelled.TrySetResult(); }
        });
        using var host = new ExternalGatewayFactory(chat);
        var firstBody = first.Body();
        var nextBody = next.Body();
        Record("External-20-01", host, first, firstBody, Samples.Sign(firstBody),
            new { ExpectedReply = Samples.TimeoutFallback, LateReply = first.ReplyText,
                NextValues = next, NextBodyBase64 = Convert.ToBase64String(nextBody),
                NextSignature = Samples.Sign(nextBody), NextReply = next.ReplyText, LineStatus = 200 });
        using var client = host.CreateClient();
        try {
            await SendAsync(client, firstBody, Samples.Sign(firstBody));
            ExternalAssertions.Chat(await host.Chat.NextAsync(), first, "https://chat.test/chat/v0.1/generate-replies");
            await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(10));
            ExternalAssertions.Line(await host.Line.NextAsync(), first, Samples.TimeoutFallback);
            using var lateResponse = Samples.Ok(first.ReplyText);
            upstream.TrySetResult(lateResponse);
            await SendAsync(client, nextBody, Samples.Sign(nextBody));
            ExternalAssertions.Chat(await host.Chat.NextAsync(), next, "https://chat.test/chat/v0.1/generate-replies");
            ExternalAssertions.Line(await host.Line.NextAsync(), next, next.ReplyText);
            await FinishAsync(host, 2, 2);
        }
        finally { upstream.TrySetCanceled(); }
    }
}
