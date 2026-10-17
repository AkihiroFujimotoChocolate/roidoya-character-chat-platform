// SPDX-License-Identifier: MIT
using System.Net;
using LineBot.Api.Services;
using LineBot.Api.Tests.Support;
using Microsoft.Extensions.Logging;
namespace LineBot.Api.Tests.ExternalContracts.Line;

public sealed class ReplyHttpContractTests
{
    // External-30: Verifies LINE reply payloads and the success/failure result of each HTTP attempt.
    [Theory, Trait("Case", "External-30")]
    [InlineData(1, 200)] // External-30-01: one message; HTTP 200
    [InlineData(2, 200)] // External-30-02: two messages; HTTP 200
    [InlineData(5, 200)] // External-30-03: five messages; HTTP 200
    [InlineData(1, 400)] // External-30-04: one message; HTTP 400
    [InlineData(1, 0)]   // External-30-05: one message; transport exception
    public async Task Reply_request_and_http_result(int count, int status)
    {
        var texts = Enumerable.Range(1, count).Select(i => $"{Samples.RichText}:{i}").ToList();
        using var handler = new CaptureHandler((_, _) => status == 0
            ? Task.FromException<HttpResponseMessage>(new HttpRequestException("transport-test"))
            : Task.FromResult(Samples.Json(status == 200
                ? "{\"sentMessages\":[{\"id\":\"sent-test\",\"quoteToken\":\"quote-test\"}]}"
                : "{\"message\":\"Invalid reply token\"}", (HttpStatusCode)status)));
        using var client = handler.Client(); var logs = new LogBook();
        var service = new LineReplyService(client, Samples.Config(), logs.For<LineReplyService>());
        var success = await service.SendReplyAsync("reply-token-test", texts, "event-reply-test", true);
        Assert.Equal(status == 200, success);
        var sent = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, sent.Method);
        Assert.Equal(new Uri("https://api.line.me/v2/bot/message/reply"), sent.Uri);
        Assert.Equal("Bearer access-token-test", sent.Header("Authorization"));
        Assert.Equal("application/json; charset=utf-8", Assert.Single(sent.ContentHeaders["Content-Type"]));
        using var json = sent.Json(); var body = json.RootElement;
        Assert.Equal("reply-token-test", body.GetProperty("replyToken").GetString());
        var messages = body.GetProperty("messages").EnumerateArray().ToArray();
        Assert.Equal(texts.Count, messages.Length);
        Assert.All(messages, m => Assert.Equal("text", m.GetProperty("type").GetString()));
        Assert.Equal(texts, messages.Select(m => m.GetProperty("text").GetString()!).ToList());
        if (status != 200) Assert.Contains(logs.Entries, e => e.Level == LogLevel.Error);
        else Assert.DoesNotContain(logs.Entries, e => e.Level >= LogLevel.Warning);
    }

    // External-31: Verifies six messages are refused and logged before any LINE HTTP attempt.
    [Theory, Trait("Case", "External-31")]
    [InlineData(false)] // External-31-01: six messages on first delivery
    [InlineData(true)]  // External-31-02: six messages on redelivery
    public async Task Six_messages_are_refused_before_sending(bool redelivery)
    {
        using var handler = new CaptureHandler(); using var client = handler.Client(); var logs = new LogBook();
        var service = new LineReplyService(client, Samples.Config(), logs.For<LineReplyService>());
        Assert.False(await service.SendReplyAsync("reply-token-test",
            Enumerable.Range(1, 6).Select(i => $"reply-{i}").ToList(), "event-limit-test", redelivery));
        Assert.Empty(handler.Requests);
        var error = Assert.Single(logs.Entries.Where(e => e.Level == LogLevel.Error &&
            e.Message.Contains("messages_over_batch_limit", StringComparison.Ordinal)));
        Assert.Equal(6, error.Values["MessageCount"]);
        Assert.Equal("event-limit-test", error.Values["WebhookEventId"]);
        Assert.Equal(redelivery, error.Values["IsRedelivery"]);
        // The specification requires the existing limit value, not an added structured key.
        Assert.Contains("limit=5", error.Message, StringComparison.Ordinal);
    }
}
