// SPDX-License-Identifier: MIT
using System.Text.Json.Serialization;

namespace LineBot.Api.Models;

public class LineReplyRequest
{
    [JsonPropertyName("replyToken")]
    public string ReplyToken { get; set; } = string.Empty;

    [JsonPropertyName("messages")]
    public List<LineMessage> Messages { get; set; } = new();
}

public class LineMessage
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "text";

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}

public class QueueItem
{
    public string WebhookEventId { get; set; } = string.Empty;
    public string ReplyToken { get; set; } = string.Empty;
    public string UserKey { get; set; } = string.Empty;
    public string MessageText { get; set; } = string.Empty;
    public bool IsRedelivery { get; set; }
    public DateTime QueuedAt { get; set; } = DateTime.UtcNow;
}

public class ChatServiceRequest
{
    public string? RequestId { get; set; }
    public string MessageText { get; set; } = string.Empty;
    public string? MessageLanguage { get; set; }
    public int TimeoutSeconds { get; set; } = 20;
    public int MaxCharsPerMessage { get; set; } = 1000;
    public string ConversationId { get; set; } = string.Empty;
    public string AuthorUserId { get; set; } = string.Empty;
}

public class ChatServiceResult
{
    public string? RequestId { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<string> Messages { get; set; } = new();
    public bool FallbackUsed { get; set; }
    public ChatError? Error { get; set; }
}

public class ChatError
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}