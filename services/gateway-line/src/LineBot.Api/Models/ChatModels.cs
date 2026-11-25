// SPDX-License-Identifier: MIT
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
