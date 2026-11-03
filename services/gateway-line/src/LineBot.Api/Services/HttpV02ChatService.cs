// SPDX-License-Identifier: MIT
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LineBot.Api.Models;

namespace LineBot.Api.Services;

public class HttpV02ChatService : IChatService
{
    private const string SupportedApiVersion = "0.2";
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<HttpV02ChatService> _logger;

    public HttpV02ChatService(HttpClient httpClient, IConfiguration configuration, ILogger<HttpV02ChatService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
        ValidateConfiguredApiVersion();
    }

    public async Task<ChatServiceResult> GenerateReplyAsync(ChatServiceRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = ResolveEndpointUrl();
            using var httpRequest = BuildHttpRequest(request, url);

            var timeoutSeconds = _configuration.GetValue<int>("Chat:RequestTimeoutSeconds", 20);
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            _logger.LogInformation("Sending HTTP v0.2 chat request to {Url}, InteractionId={InteractionId}, TimeoutSeconds={TimeoutSeconds}",
                url, request.InteractionId, timeoutSeconds);

            using var httpResponse = await _httpClient.SendAsync(httpRequest, combinedCts.Token);
            var responseContent = await httpResponse.Content.ReadAsStringAsync(combinedCts.Token);
            if (httpResponse.StatusCode != HttpStatusCode.OK)
            {
                _logger.LogWarning("HTTP v0.2 chat service returned status {StatusCode}, InteractionId={InteractionId}",
                    httpResponse.StatusCode, request.InteractionId);
                return CreateProviderErrorResponse();
            }

            var chatResponse = JsonSerializer.Deserialize<ChatV02WireResponse>(responseContent);
            var outputMessage = chatResponse?.Messages is { Count: 1 } messages ? messages[0] : null;
            var outputContent = outputMessage?.Content is { Count: 1 } content ? content[0] : null;
            if (chatResponse == null ||
                !string.Equals(chatResponse.InteractionId, request.InteractionId, StringComparison.Ordinal) ||
                !string.Equals(chatResponse.Status, "ok", StringComparison.Ordinal) ||
                outputMessage == null ||
                string.IsNullOrWhiteSpace(outputMessage.Id) ||
                string.Equals(outputMessage.Id, request.InputMessageId, StringComparison.Ordinal) ||
                outputContent == null ||
                !string.Equals(outputContent.Type, "text", StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(outputContent.Text))
            {
                _logger.LogWarning("HTTP v0.2 chat service returned an invalid response, InteractionId={InteractionId}",
                    request.InteractionId);
                return CreateProviderErrorResponse();
            }

            return new ChatServiceResult
            {
                Status = "ok",
                Messages = new List<string> { outputContent!.Text! }
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "HTTP v0.2 chat request timed out, InteractionId={InteractionId}", request.InteractionId);
            return CreateProviderErrorResponse();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in HTTP v0.2 chat service, InteractionId={InteractionId}", request.InteractionId);
            return CreateProviderErrorResponse();
        }
    }

    private string ResolveEndpointUrl()
    {
        var endpoint = _configuration["Chat:Http:Endpoint"];
        var endpointTemplate = _configuration["Chat:Http:EndpointTemplate"];
        var baseUrl = _configuration["Chat:Http:BaseUrl"];
        var appBaseUrl = _configuration["App:BaseUrl"];
        string path;

        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            path = endpoint;
        }
        else if (!string.IsNullOrWhiteSpace(endpointTemplate))
        {
            path = endpointTemplate.Replace("{version}", $"v{SupportedApiVersion}");
        }
        else
        {
            path = $"/chat/v{SupportedApiVersion}/generate-replies";
        }

        if (Uri.IsWellFormedUriString(path, UriKind.Absolute))
        {
            return path;
        }

        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            return new Uri(new Uri(baseUrl), path).ToString();
        }

        if (!string.IsNullOrWhiteSpace(appBaseUrl))
        {
            return new Uri(new Uri(appBaseUrl), path).ToString();
        }

        throw new InvalidOperationException(
            "Cannot resolve chat endpoint URL: no BaseUrl or App:BaseUrl configured, and endpoint is not absolute");
    }

    private static HttpRequestMessage BuildHttpRequest(ChatServiceRequest request, string url)
    {
        var wireRequest = new
        {
            interaction_id = request.InteractionId,
            origin = new
            {
                channel_type = request.ChannelType,
                channel_instance_id = request.ChannelInstanceId
            },
            actor = new { id = request.ActorId },
            character = new { id = request.CharacterId },
            message = new
            {
                id = request.InputMessageId,
                content = new[] { new { type = "text", text = request.MessageText } }
            }
        };

        var json = JsonSerializer.Serialize(wireRequest);
        return new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private ChatServiceResult CreateProviderErrorResponse() => new()
    {
        Status = "provider_error",
        Messages = new List<string>()
    };

    private void ValidateConfiguredApiVersion()
    {
        var configuredVersion = _configuration.GetValue<string>("Chat:ApiVersion", SupportedApiVersion);
        if (!string.Equals(configuredVersion, SupportedApiVersion, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Unsupported Chat:ApiVersion '{configuredVersion}'. This service supports only v{SupportedApiVersion}.");
        }
    }

    private sealed class ChatV02WireResponse
    {
        [JsonPropertyName("interaction_id")]
        public string? InteractionId { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("messages")]
        public List<ChatV02WireMessage>? Messages { get; set; }
    }

    private sealed class ChatV02WireMessage
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("content")]
        public List<ChatV02WireContent>? Content { get; set; }
    }

    private sealed class ChatV02WireContent
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }
}
