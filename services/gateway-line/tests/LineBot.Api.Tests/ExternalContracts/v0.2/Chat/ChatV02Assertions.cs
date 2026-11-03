// SPDX-License-Identifier: MIT
using System.Text.Json;
using System.Net.Http.Headers;
using System.Text;
using LineBot.Api.Tests.Support;
using LineBot.Api.Tests.ExternalContracts.V02.Support;
namespace LineBot.Api.Tests.ExternalContracts.V02.Chat;
internal static class ChatV02Assertions
{
    internal static void AssertRequest(CapturedHttp sent, ExternalValues input, string endpoint)
    {
        Assert.Equal(HttpMethod.Post, sent.Method);
        Assert.Equal(endpoint, sent.Uri.AbsoluteUri);
        Assert.Equal("application/json", MediaTypeHeaderValue.Parse(Assert.Single(sent.ContentHeaders["Content-Type"])).MediaType);
        using var json = sent.Json();
        var b = json.RootElement;
        Assert.Equal(JsonValueKind.Object, b.ValueKind);
        Assert.Equal(new[] { "actor", "character", "interaction_id", "message", "origin" },
            b.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray());
        foreach (var field in new[] { "origin", "actor", "character", "message" })
            Assert.Equal(JsonValueKind.Object, b.GetProperty(field).ValueKind);
        Assert.Equal(JsonValueKind.String, b.GetProperty("interaction_id").ValueKind);
        Assert.True(Guid.TryParse(b.GetProperty("interaction_id").GetString(), out _));
        var origin = b.GetProperty("origin");
        Assert.Equal(JsonValueKind.String, origin.GetProperty("channel_type").ValueKind);
        Assert.Equal("line", origin.GetProperty("channel_type").GetString());
        Assert.Equal(JsonValueKind.String, origin.GetProperty("channel_instance_id").ValueKind);
        Assert.Equal(input.Destination, origin.GetProperty("channel_instance_id").GetString());
        Assert.Equal(JsonValueKind.String, b.GetProperty("actor").GetProperty("id").ValueKind);
        Assert.Equal("line-user-" + input.PersonId, b.GetProperty("actor").GetProperty("id").GetString());
        Assert.Equal(JsonValueKind.String, b.GetProperty("character").GetProperty("id").ValueKind);
        Assert.Equal(input.CharacterId, b.GetProperty("character").GetProperty("id").GetString());
        var message = b.GetProperty("message");
        Assert.Equal(JsonValueKind.String, message.GetProperty("id").ValueKind);
        Assert.True(Guid.TryParse(message.GetProperty("id").GetString(), out _));
        var part = Assert.Single(message.GetProperty("content").EnumerateArray());
        Assert.Equal(JsonValueKind.Object, part.ValueKind);
        Assert.Equal(JsonValueKind.String, part.GetProperty("type").ValueKind);
        Assert.Equal("text", part.GetProperty("type").GetString());
        Assert.Equal(JsonValueKind.String, part.GetProperty("text").ValueKind);
        Assert.Equal(input.Text, part.GetProperty("text").GetString());
        var text = sent.Uri.AbsoluteUri + string.Join("", sent.Headers.Values.SelectMany(v => v))
            + string.Join("", sent.ContentHeaders.Values.SelectMany(v => v)) + Encoding.UTF8.GetString(sent.Body);
        foreach (var secret in new[] { "synthetic-channel-secret", "access-token-test", input.ReplyToken })
            Assert.DoesNotContain(secret, text, StringComparison.Ordinal);
    }
}
