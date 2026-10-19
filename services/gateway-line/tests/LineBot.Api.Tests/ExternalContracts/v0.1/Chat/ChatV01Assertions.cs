// SPDX-License-Identifier: MIT
using System.Text.Json;
using LineBot.Api.Tests.Support;
using LineBot.Api.Tests.ExternalContracts.V01.Support;

namespace LineBot.Api.Tests.ExternalContracts.V01.Chat;

internal static class ChatV01Assertions
{
    internal static void AssertRequest(CapturedHttp sent, ExternalValues input, string endpoint)
    {
        Assert.Equal(HttpMethod.Post, sent.Method);
        Assert.Equal(endpoint, sent.Uri.AbsoluteUri);
        Assert.Equal("application/json; charset=utf-8", Assert.Single(sent.ContentHeaders["Content-Type"]));
        Assert.Equal("0.1", sent.Header("X-Chat-Api-Version"));
        Assert.False(sent.Headers.ContainsKey("Authorization"));
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
}
