// SPDX-License-Identifier: MIT
using LineBot.Api.Tests.ExternalContracts.V01.Support;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LineBot.Api.Tests.ExternalContracts.V01.Line;

internal static class LineWebhookSamples
{
    internal const string Secret = "synthetic-channel-secret";
    internal const string RichText = "  こんにちは😀\n\"引用\"\\path  ";
    internal static byte[] Webhook(bool redelivery = false, string mode = "active", string kind = "message",
        string messageKind = "text", string text = "こんにちは", string eventId = "event-test", string person = "person-test",
        string replyToken = "reply-test", string messageId = "message-test", string destination = "destination-test")
    {
        var evt = new Dictionary<string, object?> {
            ["type"] = kind, ["mode"] = mode, ["timestamp"] = 1700000000000L, ["webhookEventId"] = eventId,
            ["source"] = new { type = "user", userId = person }, ["replyToken"] = replyToken,
            ["deliveryContext"] = new { isRedelivery = redelivery }
        };
        if (kind == "message") {
            evt["message"] = messageKind == "text"
                ? (object)new { id = messageId, type = "text", text }
                : new { id = messageId, type = "image", contentProvider = new { type = "line" } };
        }
        return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { destination, events = new[] { evt } }));
    }
    internal static string Sign(byte[] bytes, string secret = Secret) =>
        Convert.ToBase64String(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), bytes));
    internal static HttpRequestMessage Incoming(byte[] bytes, string? signature) {
        var request = new HttpRequestMessage(HttpMethod.Post, "/line/webhook")
        { Content = new ByteArrayContent(bytes) };
        request.Content.Headers.ContentType = new("application/json") { CharSet = "utf-8" };
        if (signature is not null) request.Headers.Add("X-Line-Signature", signature);
        return request;
    }

    internal static byte[] Body(this ExternalValues values, bool redelivery = false, string mode = "active",
        string kind = "message", string messageKind = "text") =>
        LineWebhookSamples.Webhook(redelivery, mode, kind, messageKind, values.Text, values.EventId, values.PersonId,
            values.ReplyToken, values.MessageId, values.Destination);

}
