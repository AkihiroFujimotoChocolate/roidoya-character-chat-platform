// SPDX-License-Identifier: MIT
using System.Net.Http.Json;
using System.Text.Json;
using LineBot.Api.Processing;

namespace LineBot.Api.ConnectionManagement;

public sealed record LineConnection(
    string Id,
    string ChannelType,
    string CharacterId,
    string Destination,
    string ChannelSecret,
    ChannelAccessToken AccessToken);

public interface IConnectionManagementClient
{
    Task<LineConnection?> GetConnectionAsync(string? connectionId, CancellationToken cancellationToken = default);
    Task<string?> ResolveActorAsync(string externalId, CancellationToken cancellationToken = default);
}

public abstract class ConnectionManagementClient : IConnectionManagementClient
{
    private readonly HttpClient _client;
    private readonly IConfiguration _configuration;
    private readonly string _version;
    private readonly string _prefix;

    protected ConnectionManagementClient(HttpClient client, IConfiguration configuration, string version)
    {
        _client = client;
        _configuration = configuration;
        _version = version;
        _prefix = "ConnectionManagement:Http";
    }

    public async Task<LineConnection?> GetConnectionAsync(string? connectionId, CancellationToken cancellationToken = default)
    {
        var payload = _version == "0.1"
            ? new { }
            : (object)new { connection = new { id = connectionId } };
        using var response = await _client.PostAsJsonAsync(ResolveUrl("GetConnection", "/channel/{version}/get-connection"), payload, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        var root = document.RootElement;
        var nestedConnection = Get(root, "connection");
        var connection = _version == "0.1" || nestedConnection.ValueKind != JsonValueKind.Object
            ? root
            : nestedConnection;
        var settings = Get(connection, "settings");
        var character = Get(connection, "character");
        if (connection.ValueKind != JsonValueKind.Object || settings.ValueKind != JsonValueKind.Object)
            return null;

        var id = _version == "0.1" ? "fixed" : String(connection, "id");
        var channelType = _version == "0.1" ? "line" : String(connection, "channel_type");
        var characterId = _version == "0.1" ? string.Empty : String(character, "id");
        var destination = _version == "0.1" ? string.Empty : String(settings, "destination");
        var secret = String(settings, "channel_secret");
        var token = String(settings, "channel_access_token");
        if (string.IsNullOrWhiteSpace(id) || channelType != "line" ||
            (_version == "0.2" && (string.IsNullOrWhiteSpace(characterId) || string.IsNullOrWhiteSpace(destination))) ||
            string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(token))
            return null;

        return new LineConnection(id!, channelType!, characterId ?? string.Empty, destination ?? string.Empty, secret!, new ChannelAccessToken(token!));
    }

    public async Task<string?> ResolveActorAsync(string externalId, CancellationToken cancellationToken = default)
    {
        if (_version != "0.2")
            return null;
        using var response = await _client.PostAsJsonAsync(ResolveUrl("ResolveActor", "/channel/{version}/resolve-actor"),
            new { channel_type = "line", external_id = externalId }, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        return String(Get(document.RootElement, "actor"), "id");
    }

    private string ResolveUrl(string operation, string defaultTemplate)
    {
        var prefix = $"{_prefix}:{operation}";
        var endpoint = _configuration[$"{prefix}:Endpoint"];
        var template = _configuration[$"{prefix}:EndpointTemplate"];
        var baseUrl = _configuration[$"{_prefix}:BaseUrl"];
        if (string.IsNullOrWhiteSpace(endpoint))
            endpoint = string.IsNullOrWhiteSpace(template) ? defaultTemplate : template;
        var path = endpoint!.Replace("{version}", $"v{_version}", StringComparison.Ordinal);
        if (Uri.TryCreate(path, UriKind.Absolute, out var absolute))
            return absolute.ToString();

        var effectiveBase = string.IsNullOrWhiteSpace(baseUrl) ? _configuration["App:BaseUrl"] : baseUrl;
        if (string.IsNullOrWhiteSpace(effectiveBase))
            throw new InvalidOperationException($"Cannot resolve {operation} URL without a base URL or absolute endpoint.");
        return new Uri(new Uri(effectiveBase), path).ToString();
    }

    private static JsonElement Get(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) ? value : default;

    private static string? String(JsonElement element, string property)
    {
        var value = Get(element, property);
        return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }
}

public sealed class V01ConnectionManagementClient(
    [Microsoft.Extensions.DependencyInjection.FromKeyedServices("connection-0.1")] HttpClient client,
    IConfiguration configuration)
    : ConnectionManagementClient(client, configuration, "0.1");

public sealed class V02ConnectionManagementClient(
    [Microsoft.Extensions.DependencyInjection.FromKeyedServices("connection-0.2")] HttpClient client,
    IConfiguration configuration)
    : ConnectionManagementClient(client, configuration, "0.2");
