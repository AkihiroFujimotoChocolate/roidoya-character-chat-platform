// SPDX-License-Identifier: MIT
using System.Text.Json.Serialization;

namespace LineBot.Api.Models;

public class WebhookEvent
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("mode")]
    public string Mode { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }

    [JsonPropertyName("webhookEventId")]
    public string WebhookEventId { get; set; } = string.Empty;

    [JsonPropertyName("source")]
    public EventSource Source { get; set; } = new();

    [JsonPropertyName("replyToken")]
    public string? ReplyToken { get; set; }

    [JsonPropertyName("message")]
    public Message? Message { get; set; }

    [JsonPropertyName("deliveryContext")]
    public DeliveryContext? DeliveryContext { get; set; }
}

public class EventSource
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("userId")]
    public string? UserId { get; set; }

    [JsonPropertyName("groupId")]
    public string? GroupId { get; set; }

    [JsonPropertyName("roomId")]
    public string? RoomId { get; set; }
}

public class Message
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("quoteToken")]
    public string? QuoteToken { get; set; }
}

public class DeliveryContext
{
    [JsonPropertyName("isRedelivery")]
    public bool IsRedelivery { get; set; }
}

public class WebhookRequestBody
{
    [JsonPropertyName("destination")]
    public string Destination { get; set; } = string.Empty;

    [JsonPropertyName("events")]
    public List<WebhookEvent> Events { get; set; } = new();
}