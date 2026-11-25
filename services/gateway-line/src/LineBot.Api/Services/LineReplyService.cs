// SPDX-License-Identifier: MIT
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using LineBot.Api.Models;
using LineBot.Api.Processing;

namespace LineBot.Api.Services;

public sealed class LineReplyService : ILineReplyService
{
    private const string ReplyEndpoint = "https://api.line.me/v2/bot/message/reply";
    private readonly HttpClient _httpClient;
    private readonly ILogger<LineReplyService> _logger;

    public LineReplyService(HttpClient httpClient, ILogger<LineReplyService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<bool> SendReplyAsync(
        LineReplyContext context,
        List<string> messages,
        CancellationToken cancellationToken = default)
    {
        if (messages.Count is 0 or > 5)
        {
            _logger.LogWarning("LINE reply message count is outside the supported range {MessageCount}, WebhookEventId={WebhookEventId}",
                messages.Count, context.WebhookEventId);
            return false;
        }

        var requestBody = new LineReplyRequest
        {
            ReplyToken = context.ReplyToken.Value,
            Messages = messages.Select(text => new LineMessage { Type = "text", Text = text }).ToList()
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, ReplyEndpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(requestBody,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }), Encoding.UTF8, "application/json")
        };
        request.Content.Headers.ContentType!.CharSet = "utf-8";
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.ChannelAccessToken.Value);

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.OK)
                return true;
            var detail = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("LINE reply failed, StatusCode={StatusCode}, WebhookEventId={WebhookEventId}, Error={Error}",
                response.StatusCode, context.WebhookEventId, detail);
            return false;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LINE reply failed, WebhookEventId={WebhookEventId}", context.WebhookEventId);
            return false;
        }
    }
}
