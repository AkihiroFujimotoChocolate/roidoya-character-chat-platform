// SPDX-License-Identifier: MIT
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LineBot.Api.Chat;

namespace LineBot.Api.Contracts.V0_2;

public sealed class HttpChatService : IChatService<ChatServiceRequest>
{
    private readonly HttpClient _client;
    private readonly IConfiguration _configuration;
    private readonly ILogger<HttpChatService> _logger;

    public HttpChatService(
        [Microsoft.Extensions.DependencyInjection.FromKeyedServices] HttpClient client,
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
            var wireRequest = new
            {
                interaction_id = request.InteractionId,
                origin = new { channel_type = request.ChannelType, channel_instance_id = request.ChannelInstanceId },
                actor = new { id = request.ActorId },
                character = new { id = request.CharacterId },
                message = new { id = request.InputMessageId, content = new[] { new { type = "text", text = request.MessageText } } }
            };
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, ResolveEndpointUrl())
            {
                Content = new StringContent(JsonSerializer.Serialize(wireRequest), Encoding.UTF8, "application/json")
            };
            using var response = await _client.SendAsync(httpRequest, linked.Token);
            if (response.StatusCode != HttpStatusCode.OK)
                return ProviderError();
            var body = await response.Content.ReadAsStringAsync(linked.Token);
            var parsed = JsonSerializer.Deserialize<ChatV02Response>(body);
            var output = parsed?.Messages is { Count: 1 } messages ? messages[0] : null;
            var content = output?.Content is { Count: 1 } contents ? contents[0] : null;
            if (parsed?.InteractionId != request.InteractionId || parsed.Status != "ok" ||
                string.IsNullOrWhiteSpace(output?.Id) || output.Id == request.InputMessageId ||
                content?.Type != "text" || string.IsNullOrWhiteSpace(content.Text))
                return ProviderError();
            return new ChatServiceResult { Status = "ok", Messages = new List<string> { content.Text } };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "c0.2 Chat request timed out, InteractionId={InteractionId}", request.InteractionId);
            return ProviderError();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "c0.2 Chat request failed, InteractionId={InteractionId}", request.InteractionId);
            return ProviderError();
        }
    }

    private string ResolveEndpointUrl()
    {
        var endpoint = _configuration["Chat:Http:Endpoint"];
        var template = _configuration["Chat:Http:EndpointTemplate"];
        var path = string.IsNullOrWhiteSpace(endpoint)
            ? (string.IsNullOrWhiteSpace(template) ? "/chat/{version}/generate-replies" : template)
            : endpoint;
        path = path!.Replace("{version}", "v0.2", StringComparison.Ordinal);
        if (Uri.TryCreate(path, UriKind.Absolute, out var absolute) &&
            (absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps))
            return absolute.ToString();
        var baseUrl = _configuration["Chat:Http:BaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
            baseUrl = _configuration["App:BaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException("No Chat base URL is configured.");
        return new Uri(new Uri(baseUrl), path).ToString();
    }

    private static ChatServiceResult ProviderError() => new() { Status = "provider_error", Messages = new List<string>() };

    private sealed class ChatV02Response
    {
        [JsonPropertyName("interaction_id")] public string? InteractionId { get; set; }
        [JsonPropertyName("status")] public string? Status { get; set; }
        [JsonPropertyName("messages")] public List<ChatV02Message>? Messages { get; set; }
    }
    private sealed class ChatV02Message
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("content")] public List<ChatV02Content>? Content { get; set; }
    }
    private sealed class ChatV02Content
    {
        [JsonPropertyName("type")] public string? Type { get; set; }
        [JsonPropertyName("text")] public string? Text { get; set; }
    }
}
