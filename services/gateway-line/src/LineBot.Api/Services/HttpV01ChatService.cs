// SPDX-License-Identifier: MIT
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LineBot.Api.Models;

namespace LineBot.Api.Services;

public class HttpV01ChatService : IChatService
{
    private const string SupportedApiVersion = "0.1";
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<HttpV01ChatService> _logger;

    public HttpV01ChatService(HttpClient httpClient, IConfiguration configuration, ILogger<HttpV01ChatService> logger)
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
            var httpRequest = BuildHttpRequest(request, url);
            
            var timeoutSeconds = _configuration.GetValue<int>("Chat:RequestTimeoutSeconds", 20);
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            _logger.LogInformation("Sending HTTP chat request to {Url}, RequestId={RequestId}, TimeoutSeconds={TimeoutSeconds}", 
                url, request.RequestId ?? "unknown", timeoutSeconds);

            var httpResponse = await _httpClient.SendAsync(httpRequest, combinedCts.Token);
            var responseContent = await httpResponse.Content.ReadAsStringAsync(combinedCts.Token);

            _logger.LogInformation("Received HTTP chat response, Status={StatusCode}, ContentLength={ContentLength}, RequestId={RequestId}", 
                httpResponse.StatusCode, responseContent.Length, request.RequestId ?? "unknown");

            if (!httpResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("HTTP chat service returned error status {StatusCode}, RequestId={RequestId}", 
                    httpResponse.StatusCode, request.RequestId ?? "unknown");
                return CreateProviderErrorResponse(request.RequestId, "http_error", $"HTTP {(int)httpResponse.StatusCode}");
            }

            var chatResponse = JsonSerializer.Deserialize<ChatV01WireResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (chatResponse == null)
            {
                _logger.LogWarning("Failed to deserialize chat response, RequestId={RequestId}", request.RequestId ?? "unknown");
                return CreateProviderErrorResponse(request.RequestId, "invalid_response", "Failed to parse response");
            }

            // Handle provider error or content filtered responses
            if (chatResponse.Status == "provider_error" || chatResponse.Status == "content_filtered")
            {
                var fallbackText = GetFallbackText(chatResponse.Status);

                _logger.LogInformation("Chat service returned {Status}, using fallback text, RequestId={RequestId}", 
                    chatResponse.Status, request.RequestId ?? "unknown");

                return new ChatServiceResult
                {
                    RequestId = chatResponse.RequestId,
                    Status = chatResponse.Status,
                    Messages = new List<string> { fallbackText },
                    FallbackUsed = true,
                    Error = chatResponse.Error is null
                        ? null
                        : new ChatError
                        {
                            Code = chatResponse.Error.Code,
                            Message = chatResponse.Error.Message
                        }
                };
            }

            return new ChatServiceResult
            {
                RequestId = chatResponse.RequestId,
                Status = chatResponse.Status,
                Messages = chatResponse.Messages ?? new List<string>(),
                FallbackUsed = chatResponse.FallbackUsed,
                Error = chatResponse.Error is null
                    ? null
                    : new ChatError
                    {
                        Code = chatResponse.Error.Code,
                        Message = chatResponse.Error.Message
                    }
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("HTTP chat request was cancelled, RequestId={RequestId}", request.RequestId ?? "unknown");
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("HTTP chat request timed out, RequestId={RequestId}", request.RequestId ?? "unknown");
            var fallbackText = GetFallbackText("timeout");
            return new ChatServiceResult
            {
                RequestId = request.RequestId,
                Status = "provider_error",
                Messages = new List<string> { fallbackText },
                FallbackUsed = true,
                Error = new ChatError
                {
                    Code = "timeout",
                    Message = "Request timed out"
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in HTTP chat service, RequestId={RequestId}", request.RequestId ?? "unknown");
            return CreateProviderErrorResponse(request.RequestId, "internal_error", ex.Message);
        }
    }

    private string ResolveEndpointUrl()
    {
        var endpoint = _configuration["Chat:Http:Endpoint"];
        var endpointTemplate = _configuration["Chat:Http:EndpointTemplate"];
        var baseUrl = _configuration["Chat:Http:BaseUrl"];
        var appBaseUrl = _configuration["App:BaseUrl"];
        string path;

        // Step 1: Determine the path to use
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
            // Default template
            path = $"/chat/v{SupportedApiVersion}/generate-replies";
        }

        // Step 2: Check if path is absolute URL
        if (Uri.IsWellFormedUriString(path, UriKind.Absolute))
        {
            return path;
        }

        // Step 3: Resolve relative path
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

    private HttpRequestMessage BuildHttpRequest(ChatServiceRequest request, string url)
    {
        // Optionally prepend a prefix to the author user ID
        var userIdPrefix = _configuration.GetValue<string>("Chat:AuthorUserIdPrefix", "line-");

        // Build the v0.1 wire protocol request
        var wireRequest = new
        {
            request_id = request.RequestId ?? Guid.NewGuid().ToString(),
            event_id = "", // Not used in current implementation
            origin = new { platform = "line" },
            conversation = new { id = request.ConversationId },
            author = new { user_id = userIdPrefix + request.AuthorUserId },
            message = new 
            { 
                text = request.MessageText,
                language = request.MessageLanguage
            },
            limits = new
            {
                timeout_seconds = request.TimeoutSeconds,
                max_chars_per_message = request.MaxCharsPerMessage
            }
        };

        var json = JsonSerializer.Serialize(wireRequest, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        // Add version header if configured
        var versionHeaderName = _configuration.GetValue<string>("Chat:Http:VersionHeaderName", "X-Chat-Api-Version");
        if (!string.IsNullOrWhiteSpace(versionHeaderName))
        {
            httpRequest.Headers.Add(versionHeaderName, SupportedApiVersion);
        }

        // Add API key if configured
        var apiKey = _configuration["Chat:Http:ApiKey"];
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            httpRequest.Headers.Add("Authorization", $"Bearer {apiKey}");
        }

        // Add additional headers if configured
        var additionalHeadersJson = _configuration["Chat:Http:AdditionalHeaders"];
        if (!string.IsNullOrWhiteSpace(additionalHeadersJson))
        {
            try
            {
                var additionalHeaders = JsonSerializer.Deserialize<Dictionary<string, string>>(additionalHeadersJson);
                if (additionalHeaders != null)
                {
                    foreach (var header in additionalHeaders)
                    {
                        httpRequest.Headers.Add(header.Key, header.Value);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse Chat:Http:AdditionalHeaders JSON");
            }
        }

        return httpRequest;
    }

    private ChatServiceResult CreateProviderErrorResponse(string? requestId, string errorCode, string errorMessage)
    {
        var fallbackText = GetFallbackText("default");
        return new ChatServiceResult
        {
            RequestId = requestId,
            Status = "provider_error",
            Messages = new List<string> { fallbackText },
            FallbackUsed = true,
            Error = new ChatError
            {
                Code = errorCode,
                Message = errorMessage
            }
        };
    }

    private string GetFallbackText(string errorType)
    {
        return errorType.ToLowerInvariant() switch
        {
            "timeout" => _configuration["Chat:Fallbacks:Timeout"] ?? 
                        _configuration["Chat:Fallbacks:Default"] ?? 
                        "The service is temporarily unavailable.",
            "content_filtered" => _configuration["Chat:Fallbacks:ContentFiltered"] ?? 
                                 _configuration["Chat:Fallbacks:Default"] ?? 
                                 "The service is temporarily unavailable.",
            _ => _configuration["Chat:Fallbacks:Default"] ?? 
                "The service is temporarily unavailable."
        };
    }

    private void ValidateConfiguredApiVersion()
    {
        var configuredVersion = _configuration.GetValue<string>("Chat:ApiVersion", SupportedApiVersion);
        if (!string.Equals(configuredVersion, SupportedApiVersion, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Unsupported Chat:ApiVersion '{configuredVersion}'. This service supports only v{SupportedApiVersion}.");
        }
    }

    private sealed class ChatV01WireResponse
    {
        [JsonPropertyName("request_id")]
        public string? RequestId { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("messages")]
        public List<string>? Messages { get; set; }

        [JsonPropertyName("fallback_used")]
        public bool FallbackUsed { get; set; }

        [JsonPropertyName("error")]
        public ChatV01WireError? Error { get; set; }
    }

    private sealed class ChatV01WireError
    {
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}