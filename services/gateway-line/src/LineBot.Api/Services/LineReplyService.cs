// SPDX-License-Identifier: MIT
using System.Text;
using System.Text.Json;
using LineBot.Api.Models;

namespace LineBot.Api.Services;

public interface ILineReplyService
{
    Task<bool> SendReplyAsync(string replyToken, List<string> messages, string webhookEventId, bool isRedelivery, CancellationToken cancellationToken = default);
}

public class LineReplyService : ILineReplyService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LineReplyService> _logger;
    private readonly string _channelAccessToken;
    private readonly JsonSerializerOptions _jsonOptions;

    public LineReplyService(HttpClient httpClient, IConfiguration configuration, ILogger<LineReplyService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
        _channelAccessToken = configuration["Line:ChannelAccessToken"] ?? throw new InvalidOperationException("Line:ChannelAccessToken not configured");
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        // Configure HttpClient
        _httpClient.BaseAddress = new Uri("https://api.line.me/");
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_channelAccessToken}");
    }

    public async Task<bool> SendReplyAsync(string replyToken, List<string> messages, string webhookEventId, bool isRedelivery, CancellationToken cancellationToken = default)
    {
        try
        {
            // Check message count limit (≤5 messages per reply)
            if (messages.Count > 5)
            {
                _logger.LogError("messages_over_batch_limit: Attempted to send {MessageCount} messages, limit=5. WebhookEventId={WebhookEventId}, IsRedelivery={IsRedelivery}", 
                    messages.Count, webhookEventId, isRedelivery);
                return false;
            }

            if (messages.Count == 0)
            {
                _logger.LogWarning("No messages to send for WebhookEventId={WebhookEventId}", webhookEventId);
                return false;
            }

            var lineMessages = messages.Select(text => new LineMessage { Type = "text", Text = text }).ToList();
            var request = new LineReplyRequest
            {
                ReplyToken = replyToken,
                Messages = lineMessages
            };

            var jsonContent = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            _logger.LogInformation("Sending reply with {MessageCount} messages for WebhookEventId={WebhookEventId}, IsRedelivery={IsRedelivery}", 
                messages.Count, webhookEventId, isRedelivery);

            var response = await _httpClient.PostAsync("v2/bot/message/reply", content, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully sent reply for WebhookEventId={WebhookEventId}, IsRedelivery={IsRedelivery}", 
                    webhookEventId, isRedelivery);
                return true;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var tokenHash = GetReplyTokenHash(replyToken);
                
                _logger.LogError("reply_token_invalid_or_expired: Failed to send reply. WebhookEventId={WebhookEventId}, IsRedelivery={IsRedelivery}, TokenHash={TokenHash}, StatusCode={StatusCode}, Error={Error}", 
                    webhookEventId, isRedelivery, tokenHash, response.StatusCode, errorContent);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while sending reply for WebhookEventId={WebhookEventId}, IsRedelivery={IsRedelivery}", 
                webhookEventId, isRedelivery);
            return false;
        }
    }

    private string GetReplyTokenHash(string replyToken)
    {
        // Return last 8 characters for logging (avoid logging full token)
        return replyToken.Length > 8 ? $"...{replyToken.Substring(replyToken.Length - 8)}" : replyToken;
    }
}