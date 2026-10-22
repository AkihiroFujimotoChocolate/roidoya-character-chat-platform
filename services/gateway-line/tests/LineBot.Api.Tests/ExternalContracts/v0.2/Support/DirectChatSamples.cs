// SPDX-License-Identifier: MIT
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LineBot.Api.Tests.Support;

namespace LineBot.Api.Tests.ExternalContracts.V02.Support;

internal sealed record DirectChatValues(string Text, string UserId, string EventId,
    string ReplyToken, string MessageId, string Destination, string CharacterId, string ReplyText, string OutputId);

internal static class DirectChatSamples
{
    internal const string Secret = "synthetic-v02-secret";
    internal const string AccessToken = "synthetic-v02-access-token";
    internal const string RichText = "  日本語😀\n\"quote\"\\path  ";
    internal static DirectChatValues Create() => new(RichText, "U" + Guid.NewGuid().ToString("N"),
        GeneratedValues.Id("event"), GeneratedValues.Id("reply-token"),
        GeneratedValues.Id("input-message"), "U" + Guid.NewGuid().ToString("N"),
        GeneratedValues.Id("character"), "応答😀\n\"reply\"\\path", GeneratedValues.Id("output-message"));

    internal static DirectChatValues FromJson(string json) => GeneratedValues.Read<DirectChatValues>(json,
        "Text", "UserId", "EventId", "ReplyToken", "MessageId", "Destination", "CharacterId", "ReplyText", "OutputId");

    internal static object ResponseDescription(DirectChatValues v) => new {
        interaction_id = "copied from captured request", status = "ok",
        messages = new[] { new { id = v.OutputId, content = new[] { new { type = "text", text = v.ReplyText } } } }
    };

    internal static byte[] Body(DirectChatValues v, string mode = "active", string sourceType = "user",
        string eventType = "message", string messageType = "text")
    {
        var source = new Dictionary<string, object?> { ["type"] = sourceType, ["userId"] = v.UserId };
        if (sourceType == "group") source["groupId"] = "G-synthetic";
        if (sourceType == "room") source["roomId"] = "R-synthetic";
        var evt = new Dictionary<string, object?> {
            ["type"] = eventType, ["mode"] = mode, ["timestamp"] = 1700000000000L,
            ["webhookEventId"] = v.EventId, ["source"] = source,
            ["replyToken"] = v.ReplyToken, ["deliveryContext"] = new { isRedelivery = false }
        };
        if (eventType == "message") evt["message"] = messageType == "text"
            ? (object)new { id = v.MessageId, type = "text", text = v.Text }
            : new { id = v.MessageId, type = "image", contentProvider = new { type = "line" } };
        return JsonSerializer.SerializeToUtf8Bytes(new { destination = v.Destination, events = new[] { evt } });
    }

    internal static string Sign(byte[] bytes, string secret = Secret) =>
        Convert.ToBase64String(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), bytes));

    internal static HttpRequestMessage Incoming(byte[] bytes, string? signature)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/line/webhook") { Content = new ByteArrayContent(bytes) };
        request.Content.Headers.ContentType = new("application/json") { CharSet = "utf-8" };
        if (signature is not null) request.Headers.Add("X-Line-Signature", signature);
        return request;
    }

    internal static HttpResponseMessage Ok(CapturedHttp request, DirectChatValues v)
    {
        using var json = request.Json();
        var id = json.RootElement.TryGetProperty("interaction_id", out var field) ? field.GetString() : "baseline-has-no-interaction";
        return HttpResponses.Json(JsonSerializer.Serialize(new {
            interaction_id = id, status = "ok",
            messages = new[] { new { id = v.OutputId, content = new[] { new { type = "text", text = v.ReplyText } } } }
        }));
    }

    internal static HttpResponseMessage LineOk() => HttpResponses.Json("{\"sentMessages\":[{\"id\":\"sent-synthetic\"}]}");
}
