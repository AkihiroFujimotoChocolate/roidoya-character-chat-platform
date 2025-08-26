// SPDX-License-Identifier: MIT
using LineBot.Api.Models;

namespace LineBot.Api.Services;

public static class WebhookHelper
{
    public static string GetUserKey(EventSource source)
    {
        // Create a unique key for user locking
        return source.Type switch
        {
            "user" => $"user:{source.UserId}",
            "group" => $"group:{source.GroupId}",
            "room" => $"room:{source.RoomId}",
            _ => $"unknown:{source.Type}"
        };
    }

    public static string GetConversationId(EventSource source)
    {
        // Create a conversation identifier
        return source.Type switch
        {
            "user" => source.UserId ?? "unknown-user",
            "group" => source.GroupId ?? "unknown-group", 
            "room" => source.RoomId ?? "unknown-room",
            _ => "unknown-conversation"
        };
    }

    public static bool ShouldProcessEvent(WebhookEvent webhookEvent)
    {
        // Only process message events that are not in standby mode
        return webhookEvent.Type == "message" && 
               webhookEvent.Mode != "standby" &&
               webhookEvent.Message?.Type == "text" &&
               !string.IsNullOrWhiteSpace(webhookEvent.Message.Text) &&
               !string.IsNullOrWhiteSpace(webhookEvent.ReplyToken);
    }

    public static List<string> GetRandomMessages(IConfiguration configuration, string configKey, int count = 1)
    {
        var messages = configuration.GetSection(configKey).Get<List<string>>() ?? new List<string>();
        if (messages.Count == 0)
        {
            return new List<string> { "Please try again later." };
        }

        var random = new Random();
        var result = new List<string>();
        for (int i = 0; i < count; i++)
        {
            result.Add(messages[random.Next(messages.Count)]);
        }
        return result;
    }
}