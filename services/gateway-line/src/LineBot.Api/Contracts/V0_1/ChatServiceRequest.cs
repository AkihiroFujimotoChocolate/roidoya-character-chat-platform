// SPDX-License-Identifier: MIT
using System.Text.Json.Serialization;
using LineBot.Api.Chat;

namespace LineBot.Api.Contracts.V0_1;

public class ChatServiceRequest : IChatServiceRequest
{
    [JsonPropertyName("request_id")]
    public string? RequestId { get; set; }

    [JsonPropertyName("message_text")]
    public string MessageText { get; set; } = string.Empty;

    [JsonPropertyName("conversation_id")]
    public string ConversationId { get; set; } = string.Empty;

    [JsonPropertyName("author_user_id")]
    public string AuthorUserId { get; set; } = string.Empty;

    [JsonPropertyName("timeout_seconds")]
    public int TimeoutSeconds { get; set; }

    [JsonPropertyName("max_chars_per_message")]
    public int MaxCharsPerMessage { get; set; }
}
