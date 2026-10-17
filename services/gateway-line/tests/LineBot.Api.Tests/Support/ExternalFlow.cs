// SPDX-License-Identifier: MIT
using System.Net;
using System.Text.Json;
using Xunit.Abstractions;
namespace LineBot.Api.Tests.Support;

internal sealed record ExternalValues(string Text, string PersonId, string EventId,
    string ReplyToken, string MessageId, string Destination, string ReplyText)
{
    internal byte[] Body(bool redelivery = false, string mode = "active",
        string kind = "message", string messageKind = "text") =>
        Samples.Webhook(redelivery, mode, kind, messageKind, Text, EventId, PersonId,
            ReplyToken, MessageId, Destination);
}
internal static class ExternalSamples
{
    internal static ExternalValues Random(string? text = null) => Fixed(new(
        text ?? GeneratedValues.Text(), GeneratedValues.Id("person"), GeneratedValues.Id("event"),
        GeneratedValues.Id("reply-token"), GeneratedValues.Id("message"),
        GeneratedValues.Id("destination"), "返信😀\n\"引用\"\\path-" + GeneratedValues.Id("reply")));
    internal static ExternalValues Fixed(ExternalValues values) => values;
    internal static ExternalValues FromJson(string json) => Fixed(
        GeneratedValues.Read<ExternalValues>(json, "Text", "PersonId", "EventId",
            "ReplyToken", "MessageId", "Destination", "ReplyText"));
}
internal static class ExternalAssertions
{
    internal static void Chat(CapturedHttp sent, ExternalValues input, string endpoint, string? key = null)
    {
        Assert.Equal(HttpMethod.Post, sent.Method);
        Assert.Equal(endpoint, sent.Uri.AbsoluteUri);
        Assert.Equal("application/json; charset=utf-8", Assert.Single(sent.ContentHeaders["Content-Type"]));
        Assert.Equal("0.1", sent.Header("X-Chat-Api-Version"));
        if (key is null) Assert.False(sent.Headers.ContainsKey("Authorization"));
        else Assert.Equal("Bearer " + key, sent.Header("Authorization"));
        using var json = sent.Json();
        var b = json.RootElement;
        Assert.Equal(new[] { "author", "conversation", "event_id", "limits", "message", "origin", "request_id" },
            b.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray());
        Assert.Equal(JsonValueKind.String, b.GetProperty("request_id").ValueKind);
        Assert.False(string.IsNullOrWhiteSpace(b.GetProperty("request_id").GetString()));
        Assert.Equal("", b.GetProperty("event_id").GetString());
        Assert.Equal("line", b.GetProperty("origin").GetProperty("platform").GetString());
        Assert.Equal("user-" + input.PersonId, b.GetProperty("conversation").GetProperty("id").GetString());
        Assert.Equal("line-user-" + input.PersonId, b.GetProperty("author").GetProperty("user_id").GetString());
        Assert.Equal(input.Text, b.GetProperty("message").GetProperty("text").GetString());
        Assert.Equal(JsonValueKind.Null, b.GetProperty("message").GetProperty("language").ValueKind);
        Assert.Equal(20, b.GetProperty("limits").GetProperty("timeout_seconds").GetInt32());
        Assert.Equal(1000, b.GetProperty("limits").GetProperty("max_chars_per_message").GetInt32());
    }
    internal static void Line(CapturedHttp sent, ExternalValues input, params string[] messages)
    {
        Assert.Equal(HttpMethod.Post, sent.Method);
        Assert.Equal("https://api.line.me/v2/bot/message/reply", sent.Uri.AbsoluteUri);
        Assert.Equal("Bearer " + Samples.AccessToken, sent.Header("Authorization"));
        Assert.Equal("application/json; charset=utf-8", Assert.Single(sent.ContentHeaders["Content-Type"]));
        using var json = sent.Json();
        var b = json.RootElement;
        Assert.Equal(input.ReplyToken, b.GetProperty("replyToken").GetString());
        var actual = b.GetProperty("messages").EnumerateArray().ToArray();
        Assert.Equal(messages.Length, actual.Length);
        for (var i = 0; i < messages.Length; i++) {
            Assert.Equal("text", actual[i].GetProperty("type").GetString());
            Assert.Equal(messages[i], actual[i].GetProperty("text").GetString());
        }
    }
}
public abstract class ExternalFlowTest : DiagnosticTest
{
    protected ExternalFlowTest(ITestOutputHelper output) : base(output) { }
    // Record exact bytes before starting the application or making the request.
    private protected void Record(string id, ExternalGatewayFactory host, ExternalValues input,
        byte[] bytes, string? signature, object scenario) =>
        Trace(id, new { Values = input, BodyBase64 = Convert.ToBase64String(bytes),
            Signature = signature, Settings = host.Settings, Scenario = scenario,
            WaitLimitSeconds = 10, QuietWindowMilliseconds = 250,
            NormalChatMetadata = new { request_id = "correlation-test", fallback_used = false },
            NormalLineResponse = "{\"sentMessages\":[{\"id\":\"sent-test\",\"quoteToken\":\"quote-test\"}]}" });

    private protected static async Task SendAsync(HttpClient client, byte[] body, string? signature)
    {
        using var request = Samples.Incoming(body, signature);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var response = await client.SendAsync(request, deadline.Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
    // A finite observation window detects extra HTTP calls; it is not a job-completion signal.
    private protected static async Task FinishAsync(ExternalGatewayFactory host, int chatCount, int lineCount)
    {
        await Task.WhenAll(host.Chat.ExpectQuietAsync(), host.Line.ExpectQuietAsync());
        await host.StopHostAsync();
        Assert.Equal(chatCount, host.Chat.Requests.Count);
        Assert.Equal(lineCount, host.Line.Requests.Count);
    }
}
