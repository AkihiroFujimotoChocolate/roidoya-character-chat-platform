// SPDX-License-Identifier: MIT
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LineBot.Api.Contracts;
using LineBot.Api.Models;
using LineBot.Api.Services;

namespace LineBot.Api.Contracts.V0_1;

public sealed class HttpChatService : IChatService<ChatServiceRequest>
{
    private const string Version = "0.1";
    private readonly HttpClient _client;
    private readonly IConfiguration _configuration;
    private readonly ILogger<HttpChatService> _logger;

    public HttpChatService(
        [Microsoft.Extensions.DependencyInjection.FromKeyedServices("0.1")] HttpClient client,
        IConfiguration configuration,
        ILogger<HttpChatService> logger)
    {
        _client = client;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<ChatServiceResult> GenerateReplyAsync(ChatServiceRequest request, CancellationToken cancellationToken = default)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Max(1, _configuration.GetValue<int>("Chat:RequestTimeoutSeconds", 20))));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            using var response = await _client.SendAsync(BuildHttpRequest(request), linked.Token);
            var body = await response.Content.ReadAsStringAsync(linked.Token);
            ChatV01Response? parsed = null;
            try { parsed = JsonSerializer.Deserialize<ChatV01Response>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
            catch (JsonException) { }

            if ((response.StatusCode is HttpStatusCode.OK or HttpStatusCode.BadRequest) &&
                parsed?.Status == "provider_error")
            {
                if (parsed.Diagnostics is { ValueKind: not JsonValueKind.Undefined })
                    _logger.LogWarning("c0.1 Chat provider diagnostics for RequestId={RequestId}: {Diagnostics}",
                        request.RequestId, parsed.Diagnostics.Value.GetRawText());
                return Fallback("Default", "provider_error");
            }
            if (response.StatusCode != HttpStatusCode.OK)
                return Fallback("Default", "provider_error");
            if (parsed?.Status == "ok" && parsed.Messages is { Count: 1 } messages &&
                !string.IsNullOrWhiteSpace(messages[0]))
                return new ChatServiceResult { Status = "ok", Messages = new List<string> { messages[0] } };
            return Fallback("Default", "provider_error");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "c0.1 Chat request timed out, RequestId={RequestId}", request.RequestId);
            return Fallback("Timeout", "provider_error");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "c0.1 Chat communication failed, RequestId={RequestId}", request.RequestId);
            return Fallback("Default", "provider_error");
        }
    }

    private HttpRequestMessage BuildHttpRequest(ChatServiceRequest request)
    {
        var prefix = _configuration["Contracts:C0_1:Chat:AuthorUserIdPrefix"] ?? "line-";
        var wireRequest = new
        {
            request_id = request.RequestId ?? Guid.NewGuid().ToString(),
            event_id = "",
            origin = new { platform = "line" },
            conversation = new { id = request.ConversationId },
            author = new { user_id = prefix + request.AuthorUserId },
            message = new { text = request.MessageText, language = (string?)null },
            limits = new { timeout_seconds = request.TimeoutSeconds, max_chars_per_message = request.MaxCharsPerMessage }
        };
        var message = new HttpRequestMessage(HttpMethod.Post, ResolveEndpointUrl())
        {
            Content = new StringContent(JsonSerializer.Serialize(wireRequest), Encoding.UTF8, "application/json")
        };
        message.Content.Headers.ContentType!.CharSet = "utf-8";
        var headerName = _configuration.GetValue<string>("Chat:Http:VersionHeaderName", "X-Chat-Api-Version");
        if (!string.IsNullOrWhiteSpace(headerName))
            message.Headers.TryAddWithoutValidation(headerName, Version);
        var extra = _configuration.GetSection("Chat:Http:AdditionalHeaders").Get<Dictionary<string, string>>();
        if (extra != null)
            foreach (var header in extra)
                message.Headers.TryAddWithoutValidation(header.Key, header.Value);
        return message;
    }

    private string ResolveEndpointUrl()
    {
        var endpoint = _configuration["Chat:Http:Endpoint"];
        var template = _configuration["Chat:Http:EndpointTemplate"];
        var path = string.IsNullOrWhiteSpace(endpoint)
            ? (string.IsNullOrWhiteSpace(template) ? "/chat/{version}/generate-replies" : template)
            : endpoint;
        path = path!.Replace("{version}", "v0.1", StringComparison.Ordinal);
        if (Uri.TryCreate(path, UriKind.Absolute, out var absolute))
            return absolute.ToString();
        var baseUrl = _configuration["Chat:Http:BaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
            baseUrl = _configuration["App:BaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException("No Chat base URL is configured.");
        return new Uri(new Uri(baseUrl), path).ToString();
    }

    private ChatServiceResult Fallback(string category, string status)
    {
        var text = _configuration[$"Contracts:C0_1:Chat:Fallbacks:{category}"];
        if (string.IsNullOrWhiteSpace(text))
            text = _configuration["Contracts:C0_1:Chat:Fallbacks:Default"];
        if (string.IsNullOrWhiteSpace(text))
            text = "The service is temporarily unavailable.";
        return new ChatServiceResult { Status = status, Messages = new List<string> { text } };
    }

    private sealed class ChatV01Response
    {
        [JsonPropertyName("status")] public string? Status { get; set; }
        [JsonPropertyName("messages")] public List<string>? Messages { get; set; }
        [JsonPropertyName("diagnostics")] public JsonElement? Diagnostics { get; set; }
    }
}
