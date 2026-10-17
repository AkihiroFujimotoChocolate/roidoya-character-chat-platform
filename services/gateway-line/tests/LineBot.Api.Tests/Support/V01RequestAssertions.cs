// SPDX-License-Identifier: MIT
using System.Text.Json;
namespace LineBot.Api.Tests.Support;

// Expected wire values are fixed by the v0.1 contract, independently of production mapping.
internal static class V01RequestAssertions
{
    internal static void Body(CapturedHttp sent, string? requestId = "correlation-test", string text = "こんにちは")
    {
        Assert.Equal(HttpMethod.Post, sent.Method);
        using var json = sent.Json();
        var body = json.RootElement;
        Assert.Equal(new[] { "author", "conversation", "event_id", "limits", "message", "origin", "request_id" },
            body.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray());
        Assert.Equal(JsonValueKind.String, body.GetProperty("request_id").ValueKind);
        if (requestId is null)
            Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("request_id").GetString()));
        else
            Assert.Equal(requestId, body.GetProperty("request_id").GetString());
        Assert.Equal("", body.GetProperty("event_id").GetString());
        Assert.Equal("line", body.GetProperty("origin").GetProperty("platform").GetString());
        Assert.Equal("conversation-test", body.GetProperty("conversation").GetProperty("id").GetString());
        Assert.Equal("line-author-test", body.GetProperty("author").GetProperty("user_id").GetString());
        Assert.Equal(text, body.GetProperty("message").GetProperty("text").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("message").GetProperty("language").ValueKind);
        Assert.Equal(7, body.GetProperty("limits").GetProperty("timeout_seconds").GetInt32());
        Assert.Equal(123, body.GetProperty("limits").GetProperty("max_chars_per_message").GetInt32());
    }

    internal static void Headers(CapturedHttp sent, string? versionHeaderName = null, string? apiKey = null)
    {
        Assert.Equal("application/json; charset=utf-8", Assert.Single(sent.ContentHeaders["Content-Type"]));
        Assert.Equal("0.1", sent.Header(versionHeaderName ?? "X-Chat-Api-Version"));
        if (apiKey is null)
            Assert.False(sent.Headers.ContainsKey("Authorization"));
        else
            Assert.Equal("Bearer " + apiKey, sent.Header("Authorization"));
    }
}
