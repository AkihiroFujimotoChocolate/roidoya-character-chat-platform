// SPDX-License-Identifier: MIT
using LineBot.Api.Tests.Support;
using System.Text.Json;
using LineBot.Api.Tests.ExternalContracts.V01.Line;
using LineBot.Api.Tests.ExternalContracts.V02.Support;

namespace LineBot.Api.Tests.ExternalContracts.V02.Line;

internal static class LineReplyAssertions
{
    internal static void AssertRequest(CapturedHttp sent, ExternalValues input, params string[] messages)
    {
        Assert.Equal(HttpMethod.Post, sent.Method);
        Assert.Equal("https://api.line.me/v2/bot/message/reply", sent.Uri.AbsoluteUri);
        Assert.Equal("Bearer " + LineReplySamples.AccessToken, sent.Header("Authorization"));
        Assert.Equal("application/json; charset=utf-8", Assert.Single(sent.ContentHeaders["Content-Type"]));
        using var json = sent.Json();
        var b = json.RootElement;
        Assert.Equal(JsonValueKind.Object, b.ValueKind);
        Assert.Equal(JsonValueKind.String, b.GetProperty("replyToken").ValueKind);
        Assert.Equal(input.ReplyToken, b.GetProperty("replyToken").GetString());
        var actual = b.GetProperty("messages").EnumerateArray().ToArray();
        Assert.Equal(messages.Length, actual.Length);
        for (var i = 0; i < messages.Length; i++) {
            Assert.Equal(JsonValueKind.Object, actual[i].ValueKind);
            Assert.Equal(JsonValueKind.String, actual[i].GetProperty("type").ValueKind);
            Assert.Equal(JsonValueKind.String, actual[i].GetProperty("text").ValueKind);
            Assert.Equal("text", actual[i].GetProperty("type").GetString());
            Assert.Equal(messages[i], actual[i].GetProperty("text").GetString());
        }
    }
}
