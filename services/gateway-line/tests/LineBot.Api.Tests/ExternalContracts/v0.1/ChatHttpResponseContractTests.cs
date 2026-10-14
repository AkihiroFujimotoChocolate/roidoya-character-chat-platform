// SPDX-License-Identifier: MIT
using System.Net;
using LineBot.Api.Services;
using LineBot.Api.Tests.Support;
using Microsoft.Extensions.Logging;
namespace LineBot.Api.Tests.ExternalContracts.V01;

public sealed class ChatHttpResponseContractTests
{
    [Theory, Trait("Case", "External-15")]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Normal_response_keeps_text_order_without_metadata(int count)
    {
        var messages = new[] { "reply-a", "reply-b" }.Take(count).ToArray();
        using var handler = new CaptureHandler((_, _) => Task.FromResult(Samples.Ok(messages)));
        using var client = handler.Client();
        var service = new HttpV01ChatService(client, Samples.Config(), new LogBook().For<HttpV01ChatService>());
        Assert.Equal(messages, (await service.GenerateReplyAsync(Samples.ChatInput())).Messages);
    }

    [Fact, Trait("Case", "External-16")]
    public async Task Unknown_fields_and_optional_metadata_do_not_change_text()
    {
        var bodies = new[] {
            "{\"status\":\"ok\",\"messages\":[\"reply-a\",\"reply-b\"]}",
            "{\"request_id\":\"different-correlation\",\"status\":\"ok\",\"messages\":[\"reply-a\",\"reply-b\"],\"fallback_used\":true,\"extra\":{\"flag\":9},\"error\":{\"message\":\"diagnostic-only\"}}"
        };
        foreach (var body in bodies) {
            using var handler = new CaptureHandler((_, _) => Task.FromResult(Samples.Json(body)));
            using var client = handler.Client();
            var service = new HttpV01ChatService(client, Samples.Config(), new LogBook().For<HttpV01ChatService>());
            Assert.Equal(new[] { "reply-a", "reply-b" }, (await service.GenerateReplyAsync(Samples.ChatInput())).Messages);
        }
    }

    [Fact, Trait("Case", "External-18")]
    public async Task Provider_error_uses_default_without_diagnostic_text()
    {
        using var handler = new CaptureHandler((_, _) => Task.FromResult(Samples.Json(
            "{\"request_id\":\"correlation-test\",\"status\":\"provider_error\",\"messages\":[],\"fallback_used\":false,\"error\":{\"code\":\"runtime_error\",\"message\":\"diag-test\"}}")));
        using var client = handler.Client(); var logs = new LogBook();
        var service = new HttpV01ChatService(client, Samples.Config(), logs.For<HttpV01ChatService>());
        Assert.Equal(new[] { "default-test" }, (await service.GenerateReplyAsync(Samples.ChatInput())).Messages);
        Assert.Contains(logs.Entries, e => e.Level == LogLevel.Information && e.Values.Values.Any(v => Equals(v, "provider_error")));
    }

    [Theory, Trait("Case", "External-19")]
    [InlineData("filtered-test")]
    [InlineData("")]
    public async Task Content_filtered_preserves_explicit_fallback_including_empty(string fallback)
    {
        using var handler = new CaptureHandler((_, _) => Task.FromResult(Samples.Json(
            "{\"status\":\"content_filtered\",\"messages\":[],\"error\":{\"code\":\"filtered\",\"message\":\"diag-test\"}}")));
        using var client = handler.Client(); var logs = new LogBook();
        var service = new HttpV01ChatService(client, Samples.Config(("Chat:Fallbacks:ContentFiltered", fallback)), logs.For<HttpV01ChatService>());
        Assert.Equal(new[] { fallback }, (await service.GenerateReplyAsync(Samples.ChatInput())).Messages);
        Assert.Contains(logs.Entries, e => e.Level == LogLevel.Information && e.Values.Values.Any(v => Equals(v, "content_filtered")));
    }

    [Fact, Trait("Case", "External-20")]
    public async Task Timeout_has_one_reply_late_completion_has_none_and_next_job_recovers()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var upstream = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var lateFinished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        using var chat = new CaptureHandler(async (_, token) => {
            if (Interlocked.Increment(ref calls) != 1) return Samples.Ok("smoke-recovered");
            started.TrySetResult();
            // Model an upstream completion arriving after the gateway deadline.
            // Cancelling this await does not complete or cancel the upstream task.
            try { return await upstream.Task.WaitAsync(token); }
            finally { lateFinished.TrySetResult(); }
        });
        using var factory = new GatewayFactory(chat);
        using var gateway = factory.CreateClient();
        var firstBody = Samples.Webhook(eventId: "event-timeout");
        using var firstRequest = Samples.Incoming(firstBody, Samples.Sign(firstBody));
        using var firstAck = await gateway.SendAsync(firstRequest);
        Assert.Equal(HttpStatusCode.OK, firstAck.StatusCode);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var timeoutReply = await factory.LineHttp.NextAsync();
        using (var json = timeoutReply.Json())
            Assert.Equal(Samples.TimeoutFallback, json.RootElement.GetProperty("messages")[0].GetProperty("text").GetString());
        await factory.Queue.AfterJob.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await lateFinished.Task.WaitAsync(TimeSpan.FromSeconds(10));
        using var lateResponse = Samples.Ok("late-normal-reply");
        upstream.SetResult(lateResponse);

        var secondBody = Samples.Webhook(eventId: "event-recovery", person: "person-recovery", text: "smoke-recovery-input");
        using var secondRequest = Samples.Incoming(secondBody, Samples.Sign(secondBody));
        using var secondAck = await gateway.SendAsync(secondRequest);
        Assert.Equal(HttpStatusCode.OK, secondAck.StatusCode);
        var recoveredReply = await factory.LineHttp.NextAsync();
        using (var json = recoveredReply.Json())
            Assert.Equal("smoke-recovered", json.RootElement.GetProperty("messages")[0].GetProperty("text").GetString());
        await factory.Logs.WaitAsync(e => e.Category == typeof(MessageWorkerService).FullName &&
            e.Values.TryGetValue("WebhookEventId", out var id) && Equals(id, "event-recovery") &&
            e.Message.Contains("Successfully processed", StringComparison.Ordinal));
        await factory.StopWorkerAsync();
        Assert.Equal(2, factory.LineHttp.Requests.Count);
        Assert.Equal(2, chat.Requests.Count);
        Assert.Contains(factory.Logs.Entries, e => e.Category == typeof(HttpV01ChatService).FullName && e.Level == LogLevel.Warning);
        foreach (var sent in factory.LineHttp.Requests) {
            using var json = sent.Json();
            Assert.Single(json.RootElement.GetProperty("messages").EnumerateArray());
            Assert.NotEqual("late-normal-reply", json.RootElement.GetProperty("messages")[0].GetProperty("text").GetString());
        }
    }

    [Theory, Trait("Case", "External-21")]
    [InlineData(400)]
    [InlineData(500)]
    [InlineData(0)]
    public async Task Http_error_or_transport_failure_uses_default_and_records_failure(int status)
    {
        using var handler = new CaptureHandler((_, _) => status == 0
            ? Task.FromException<HttpResponseMessage>(new HttpRequestException("transport-test"))
            : Task.FromResult(Samples.Json("{\"status\":\"provider_error\",\"messages\":[],\"error\":{\"code\":\"fail_test\",\"message\":\"diag-test\"}}", (HttpStatusCode)status)));
        using var client = handler.Client(); var logs = new LogBook();
        var service = new HttpV01ChatService(client, Samples.Config(("Chat:Fallbacks:Default", Samples.DefaultFallback)), logs.For<HttpV01ChatService>());
        Assert.Equal(new[] { Samples.DefaultFallback }, (await service.GenerateReplyAsync(Samples.ChatInput())).Messages);
        Assert.Single(handler.Requests);
        Assert.Contains(logs.Entries, e => e.Level == (status == 0 ? LogLevel.Error : LogLevel.Warning));
    }
}
