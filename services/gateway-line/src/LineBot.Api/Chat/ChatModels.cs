// SPDX-License-Identifier: MIT
namespace LineBot.Api.Chat;

public interface IChatServiceRequest { }

public class ChatServiceResult
{
    public List<string> Messages { get; set; } = new();
    public string Status { get; set; } = string.Empty;
}
