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

public class ChatRequest
{
    public string? RequestId { get; set; }
    public string ApiVersion { get; set; } = "0.1";
    public ChatMessage Message { get; set; } = new();
    public ChatLimits Limits { get; set; } = new();
    public ChatConversation Conversation { get; set; } = new();
    public ChatAuthor Author { get; set; } = new();
}

public class ChatMessage
{
    public string Text { get; set; } = string.Empty;
    public string? Language { get; set; }
}

public class ChatLimits
{
    public int TimeoutSeconds { get; set; } = 20;
    public int MaxCharsPerMessage { get; set; } = 1000;
}

public class ChatConversation
{
    public string Id { get; set; } = string.Empty;
}

public class ChatAuthor
{
    public string UserId { get; set; } = string.Empty;
}

public class ChatResponse
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