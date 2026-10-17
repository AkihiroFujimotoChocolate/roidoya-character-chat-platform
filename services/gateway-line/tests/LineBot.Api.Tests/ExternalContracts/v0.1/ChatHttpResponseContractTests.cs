// SPDX-License-Identifier: MIT
using System.Net;
using LineBot.Api.Services;
using LineBot.Api.Tests.Support;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;
namespace LineBot.Api.Tests.ExternalContracts.V01;

public sealed class ChatHttpResponseContractTests : DiagnosticTest
{
    public ChatHttpResponseContractTests(ITestOutputHelper output) : base(output) { }

    // External-15: Verifies normal response text, order and count without adding diagnostic metadata.
    [Theory, Trait("Case", "External-15")]
    [InlineData(1)] // External-15-01: one reply message
    [InlineData(2)] // External-15-02: two reply messages
    public async Task Normal_response_keeps_text_order_without_metadata(int count)
    {
        var sample = RequestSamples.CreateRandom();
        var data = FlowSamples.CreateRandom(count);
        var messages = data.Messages;
        var settings = Samples.Config();
        Trace($"External-15-{count:00}", new { sample.Values, Messages = messages,
            ResponseRequestId = "correlation-test", Status = "ok", FallbackUsed = false }, settings);
        using var handler = new CaptureHandler((_, _) => Task.FromResult(Samples.Ok(messages)));
        using var client = handler.Client();
        var service = new HttpV01ChatService(client, settings, new LogBook().For<HttpV01ChatService>());
        Assert.Equal(messages, (await service.GenerateReplyAsync(sample.Input)).Messages);
    }

    // External-16 / External-16-01: Verifies unknown fields and optional metadata do not change reply text.
    [Fact, Trait("Case", "External-16")]
    public async Task Unknown_fields_and_optional_metadata_do_not_change_text()
    {
        var sample = RequestSamples.CreateRandom();
        var data = FlowSamples.CreateRandom();
        var settings = Samples.Config();
        var bodies = new[] {
            System.Text.Json.JsonSerializer.Serialize(new { status = "ok", messages = data.Messages }),
            System.Text.Json.JsonSerializer.Serialize(new { request_id = GeneratedValues.Id("other-request"),
                status = "ok", messages = data.Messages, fallback_used = true, extra = new { flag = 9 },
                error = new { message = "diagnostic-only" } })
        };
        Trace("External-16-01", new { sample.Values, Messages = data.Messages, ResponseBodies = bodies }, settings);
        foreach (var body in bodies) {
            using var handler = new CaptureHandler((_, _) => Task.FromResult(Samples.Json(body)));
            using var client = handler.Client();
            var service = new HttpV01ChatService(client, settings, new LogBook().For<HttpV01ChatService>());
            Assert.Equal(data.Messages, (await service.GenerateReplyAsync(sample.Input)).Messages);
        }
    }

    // External-18 / External-18-01: Verifies provider errors select the default fallback without exposing diagnostics.
    [Fact, Trait("Case", "External-18")]
    public async Task Provider_error_uses_default_without_diagnostic_text()
    {
        var sample = RequestSamples.CreateRandom();
        var data = FlowSamples.CreateRandom();
        var settings = Samples.Config(("Chat:Fallbacks:Default", data.DefaultFallback));
        var responseBody = "{\"request_id\":\"correlation-test\",\"status\":\"provider_error\",\"messages\":[],\"fallback_used\":false,\"error\":{\"code\":\"runtime_error\",\"message\":\"diag-test\"}}";
        Trace("External-18-01", new { sample.Values, ResponseBody = responseBody,
            ExpectedMessages = new[] { data.DefaultFallback } }, settings);
        using var handler = new CaptureHandler((_, _) => Task.FromResult(Samples.Json(responseBody)));
        using var client = handler.Client(); var logs = new LogBook();
        var service = new HttpV01ChatService(client, settings, logs.For<HttpV01ChatService>());
        Assert.Equal(new[] { data.DefaultFallback }, (await service.GenerateReplyAsync(sample.Input)).Messages);
        Assert.Contains(logs.Entries, e => e.Level == LogLevel.Information && e.Values.Values.Any(v => Equals(v, "provider_error")));
    }

    // External-19: Verifies content filtering preserves the configured fallback, including an explicit empty string.
    [Theory, Trait("Case", "External-19")]
    [InlineData("filtered-test")] // External-19-01: non-empty content-filtered fallback
    [InlineData("")]              // External-19-02: explicit empty content-filtered fallback
    public async Task Content_filtered_preserves_explicit_fallback_including_empty(string fallback)
    {
        var sample = RequestSamples.CreateRandom();
        var condition = fallback.Length == 0 ? "External-19-02" : "External-19-01";
        fallback = fallback.Length == 0 ? "" : GeneratedValues.Id("filtered");
        var settings = Samples.Config(("Chat:Fallbacks:ContentFiltered", fallback));
        var responseBody = "{\"status\":\"content_filtered\",\"messages\":[],\"error\":{\"code\":\"filtered\",\"message\":\"diag-test\"}}";
        Trace(condition, new { sample.Values, ResponseBody = responseBody, ExpectedMessages = new[] { fallback } }, settings);
        using var handler = new CaptureHandler((_, _) => Task.FromResult(Samples.Json(responseBody)));
        using var client = handler.Client(); var logs = new LogBook();
        var service = new HttpV01ChatService(client, settings, logs.For<HttpV01ChatService>());
        Assert.Equal(new[] { fallback }, (await service.GenerateReplyAsync(sample.Input)).Messages);
        Assert.Contains(logs.Entries, e => e.Level == LogLevel.Information && e.Values.Values.Any(v => Equals(v, "content_filtered")));
    }

    // External-20 / External-20-01: Verifies one timeout reply, no late reply and recovery on the next job.
    [Fact, Trait("Case", "External-20")]
    public async Task Timeout_has_one_reply_late_completion_has_none_and_next_job_recovers()
    {
        var first = FlowSamples.CreateRandom();
        var recovery = FlowSamples.CreateRandom();
        var recoveredText = "recovered-" + GeneratedValues.Text();
        var lateText = "late-" + GeneratedValues.Text();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var upstream = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var lateFinished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        using var chat = new CaptureHandler(async (_, token) => {
            if (Interlocked.Increment(ref calls) != 1) return Samples.Ok(recoveredText);
            started.TrySetResult();
            // Model an upstream completion arriving after the gateway deadline.
            // Cancelling this await does not complete or cancel the upstream task.
            try { return await upstream.Task.WaitAsync(token); }
            finally { lateFinished.TrySetResult(); }
        });
        using var factory = new GatewayFactory(chat);
        Trace("External-20-01", new { First = first, Recovery = recovery, recoveredText, lateText,
            FactorySettings = factory.Settings, WaitLimitSeconds = 10,
            ExpectedTimeoutText = Samples.TimeoutFallback });
        using var gateway = factory.CreateClient();
        var firstBody = first.Webhook();
        using var firstRequest = Samples.Incoming(firstBody, Samples.Sign(firstBody));
        using var firstAck = await gateway.SendAsync(firstRequest);
        Assert.Equal(HttpStatusCode.OK, firstAck.StatusCode);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var timeoutReply = await factory.LineHttp.NextAsync();
        using (var json = timeoutReply.Json())
            Assert.Equal(Samples.TimeoutFallback, json.RootElement.GetProperty("messages")[0].GetProperty("text").GetString());
        await factory.Queue.AfterJob.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await lateFinished.Task.WaitAsync(TimeSpan.FromSeconds(10));
        using var lateResponse = Samples.Ok(lateText);
        upstream.SetResult(lateResponse);

        var secondBody = recovery.Webhook();
        using var secondRequest = Samples.Incoming(secondBody, Samples.Sign(secondBody));
        using var secondAck = await gateway.SendAsync(secondRequest);
        Assert.Equal(HttpStatusCode.OK, secondAck.StatusCode);
        var recoveredReply = await factory.LineHttp.NextAsync();
        using (var json = recoveredReply.Json())
            Assert.Equal(recoveredText, json.RootElement.GetProperty("messages")[0].GetProperty("text").GetString());
        await factory.Logs.WaitAsync(e => e.Category == typeof(MessageWorkerService).FullName &&
            e.Values.TryGetValue("WebhookEventId", out var id) && Equals(id, recovery.EventId) &&
            e.Message.Contains("Successfully processed", StringComparison.Ordinal));
        await factory.StopWorkerAsync();
        Assert.Equal(2, factory.LineHttp.Requests.Count);
        Assert.Equal(2, chat.Requests.Count);
        Assert.Contains(factory.Logs.Entries, e => e.Category == typeof(HttpV01ChatService).FullName && e.Level == LogLevel.Warning);
        foreach (var sent in factory.LineHttp.Requests) {
            using var json = sent.Json();
            Assert.Single(json.RootElement.GetProperty("messages").EnumerateArray());
            Assert.NotEqual(lateText, json.RootElement.GetProperty("messages")[0].GetProperty("text").GetString());
        }
    }

    // External-21: Verifies HTTP and transport failures select the default fallback and are logged.
    [Theory, Trait("Case", "External-21")]
    [InlineData(400)] // External-21-01: HTTP 400 response
    [InlineData(500)] // External-21-02: HTTP 500 response
    [InlineData(0)]   // External-21-03: transport exception
    public async Task Http_error_or_transport_failure_uses_default_and_records_failure(int status)
    {
        var sample = RequestSamples.CreateRandom();
        var data = FlowSamples.CreateRandom();
        var settings = Samples.Config(("Chat:Fallbacks:Default", data.DefaultFallback));
        var responseBody = "{\"status\":\"provider_error\",\"messages\":[],\"error\":{\"code\":\"fail_test\",\"message\":\"diag-test\"}}";
        var condition = status == 400 ? "External-21-01" : status == 500 ? "External-21-02" : "External-21-03";
        Trace(condition, new { sample.Values, status, ResponseBody = responseBody,
            TransportException = status == 0 ? "transport-test" : null,
            ExpectedMessages = new[] { data.DefaultFallback } }, settings);
        using var handler = new CaptureHandler((_, _) => status == 0
            ? Task.FromException<HttpResponseMessage>(new HttpRequestException("transport-test"))
            : Task.FromResult(Samples.Json(responseBody, (HttpStatusCode)status)));
        using var client = handler.Client(); var logs = new LogBook();
        var service = new HttpV01ChatService(client, settings, logs.For<HttpV01ChatService>());
        Assert.Equal(new[] { data.DefaultFallback }, (await service.GenerateReplyAsync(sample.Input)).Messages);
        Assert.Single(handler.Requests);
        Assert.Contains(logs.Entries, e => e.Level == (status == 0 ? LogLevel.Error : LogLevel.Warning));
    }
}
