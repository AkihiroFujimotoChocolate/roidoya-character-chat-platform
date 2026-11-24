// SPDX-License-Identifier: MIT
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LineBot.Api.Models;

namespace LineBot.Api.Processing;

public readonly record struct LineReplyToken(string Value);
public readonly record struct ChannelAccessToken(string Value);

public sealed record LineReplyContext(
    LineReplyToken ReplyToken,
    ChannelAccessToken ChannelAccessToken,
    string WebhookEventId);

public sealed class VerifiedLineWebhook
{
    private readonly JsonElement _root;

    private VerifiedLineWebhook(byte[] body)
    {
        using var document = JsonDocument.Parse(body);
        _root = document.RootElement.Clone();
    }

    public JsonElement Root => _root;

    public static bool TryCreate(byte[] body, string? signature, string channelSecret, out VerifiedLineWebhook? webhook)
    {
        webhook = null;
        if (string.IsNullOrWhiteSpace(signature))
            return false;
        byte[] supplied;
        try { supplied = Convert.FromBase64String(signature); }
        catch (FormatException) { return false; }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(channelSecret));
        if (!CryptographicOperations.FixedTimeEquals(supplied, hmac.ComputeHash(body)))
            return false;

        webhook = new VerifiedLineWebhook(body);
        return true;
    }

    public IReadOnlyList<QueueItem> SplitEvents(string? connectionId)
    {
        if (!_root.TryGetProperty("events", out var events) || events.ValueKind != JsonValueKind.Array)
            return Array.Empty<QueueItem>();

        var items = new List<QueueItem>();
        foreach (var webhookEvent in events.EnumerateArray())
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                foreach (var property in _root.EnumerateObject())
                {
                    if (property.NameEquals("events"))
                        continue;
                    property.WriteTo(writer);
                }
                writer.WritePropertyName("event");
                webhookEvent.WriteTo(writer);
                writer.WriteEndObject();
            }

            using var document = JsonDocument.Parse(stream.ToArray());
            items.Add(new QueueItem { Webhook = document.RootElement.Clone(), ConnectionId = connectionId });
        }
        return items;
    }
}

public interface ILineEventFilter
{
    Task<bool> IsTargetAsync(QueueItem item, CancellationToken cancellationToken = default);
}

public sealed class LineEventFilter : ILineEventFilter
{
    public Task<bool> IsTargetAsync(QueueItem item, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var target = TryGetEvent(item.Webhook, out var webhookEvent) &&
                     GetString(webhookEvent, "type") == "message" &&
                     GetString(webhookEvent, "mode") != "standby" &&
                     TryGetObject(webhookEvent, "source", out var source) &&
                     GetString(source, "type") == "user" &&
                     !string.IsNullOrWhiteSpace(GetString(source, "userId")) &&
                     TryGetObject(webhookEvent, "message", out var message) &&
                     GetString(message, "type") == "text" &&
                     !string.IsNullOrWhiteSpace(GetString(message, "text")) &&
                     !string.IsNullOrWhiteSpace(GetString(webhookEvent, "replyToken"));
        return Task.FromResult(target);
    }

    internal static bool TryGetEvent(JsonElement webhook, out JsonElement webhookEvent) =>
        TryGetObject(webhook, "event", out webhookEvent);

    internal static bool TryGetObject(JsonElement element, string name, out JsonElement value)
    {
        value = default;
        return element.ValueKind == JsonValueKind.Object &&
               element.TryGetProperty(name, out value) &&
               value.ValueKind == JsonValueKind.Object;
    }

    internal static string? GetString(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}

public interface ILineReplyService
{
    Task<bool> SendReplyAsync(
        LineReplyContext context,
        List<string> messages,
        CancellationToken cancellationToken = default);
}
