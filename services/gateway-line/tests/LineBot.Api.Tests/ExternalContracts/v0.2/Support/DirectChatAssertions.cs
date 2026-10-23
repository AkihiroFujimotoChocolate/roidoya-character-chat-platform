// SPDX-License-Identifier: MIT
using System.Text.Json;
using LineBot.Api.Tests.Support;

namespace LineBot.Api.Tests.ExternalContracts.V02.Support;

internal sealed record DirectChatIds(string Interaction, string Actor, string Message);

internal static class DirectChatAssertions
{
    internal static DirectChatIds AssertRequest(CapturedHttp sent, DirectChatValues v, string endpoint)
    {
        Assert.Equal(HttpMethod.Post, sent.Method);
        Assert.Equal(endpoint, sent.Uri.AbsoluteUri);
        Assert.Equal("application/json", Assert.Single(sent.ContentHeaders["Content-Type"]).Split(';')[0]);
        if (sent.Headers.TryGetValue("X-Chat-Api-Version", out var versions)) Assert.Equal("0.2", Assert.Single(versions));
        Assert.False(sent.Headers.ContainsKey("Authorization"));
        using var json = sent.Json(); var b = json.RootElement;
        AssertFields(b, "interaction_id", "origin", "actor", "character", "message");
        AssertFields(b.GetProperty("origin"), "channel_type", "channel_instance_id");
        Assert.Equal("line", b.GetProperty("origin").GetProperty("channel_type").GetString());
        Assert.Equal(v.Destination, b.GetProperty("origin").GetProperty("channel_instance_id").GetString());
        foreach (var name in new[] { "actor", "character" }) AssertFields(b.GetProperty(name), "id");
        Assert.Equal("line-user-" + v.UserId, b.GetProperty("actor").GetProperty("id").GetString());
        Assert.Equal(v.CharacterId, b.GetProperty("character").GetProperty("id").GetString());
        var message = b.GetProperty("message"); AssertFields(message, "id", "content");
        var part = Assert.Single(message.GetProperty("content").EnumerateArray()); AssertFields(part, "type", "text");
        Assert.Equal("text", part.GetProperty("type").GetString()); Assert.Equal(v.Text, part.GetProperty("text").GetString());
        // Phase 1 uses the c0.1-style actor value; LINE credentials stay at the Gateway.
        Assert.DoesNotContain(v.ReplyToken, b.GetRawText(), StringComparison.Ordinal);
        return new(AssertNonEmpty(b.GetProperty("interaction_id")),
            AssertNonEmpty(b.GetProperty("actor").GetProperty("id")), AssertNonEmpty(message.GetProperty("id")));
    }
    internal static void AssertReply(CapturedHttp sent, DirectChatValues v)
    {
        Assert.Equal(HttpMethod.Post, sent.Method);
        Assert.Equal("https://api.line.me/v2/bot/message/reply", sent.Uri.AbsoluteUri);
        Assert.Equal("Bearer " + DirectChatSamples.AccessToken, sent.Header("Authorization"));
        Assert.Equal("application/json", Assert.Single(sent.ContentHeaders["Content-Type"]).Split(';')[0]);
        using var json = sent.Json(); var root = json.RootElement;
        Assert.Equal(v.ReplyToken, root.GetProperty("replyToken").GetString());
        var message = Assert.Single(root.GetProperty("messages").EnumerateArray());
        Assert.Equal("text", message.GetProperty("type").GetString());
        Assert.Equal(v.ReplyText, message.GetProperty("text").GetString());
    }
    private static string AssertNonEmpty(JsonElement e)
    {
        Assert.Equal(JsonValueKind.String, e.ValueKind); var value = e.GetString();
        Assert.False(string.IsNullOrWhiteSpace(value)); return value!;
    }
    private static void AssertFields(JsonElement e, params string[] fields) =>
        Assert.Equal(fields.OrderBy(x => x).ToArray(), e.EnumerateObject().Select(p => p.Name).OrderBy(x => x).ToArray());
}
