// SPDX-License-Identifier: MIT
using System.Text.Json;

namespace LineBot.Api.Models;

public class LineReplyRequest
{
    public string ReplyToken { get; set; } = string.Empty;
    public List<LineMessage> Messages { get; set; } = new();
}

public class LineMessage
{
    public string Type { get; set; } = "text";
    public string Text { get; set; } = string.Empty;
}

public class QueueItem
{
    public JsonElement Webhook { get; set; }
    public string? ConnectionId { get; set; }
}

public interface IChatServiceRequest { }

public class ChatServiceResult
{
    public List<string> Messages { get; set; } = new();
    public string Status { get; set; } = string.Empty;
}
