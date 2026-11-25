#if PHASE2_GATEWAY_TESTS
// SPDX-License-Identifier: MIT
using System.Net.Http.Headers;
using System.Text.Json;
using LineBot.Api.Tests.Support;
using LineBot.Api.Tests.ExternalContracts.V01.Line;
using LineBot.Api.Tests.ExternalContracts.V02.Support;

namespace LineBot.Api.Tests.ExternalContracts.V02.ConnectionManagement;

internal static class ConnectionSamples
{
    internal static object Connection(ExternalValues data, string? destination = null) => new
    {
        id = data.ConnectionId,
        channel_type = "line",
        character = new { id = data.CharacterId },
        settings = new
        {
            destination = destination ?? data.Destination,
            channel_secret = LineWebhookSamples.Secret,
            channel_access_token = LineReplySamples.AccessToken
        }
    };

    internal static CaptureHandler Capture(ExternalValues data, Func<string, ExternalValues>? actor = null,
        string? destination = null, Uri? get = null)
    {
        var getUri = get ?? new Uri("https://connections.test/channel/v0.2/get-connection");
        return new CaptureHandler((sent, _) =>
        {
            if (sent.Uri == getUri)
                return Task.FromResult(HttpResponses.Json(JsonSerializer.Serialize(Connection(data, destination))));
            using var json = sent.Json();
            var person = json.RootElement.GetProperty("external_id").GetString()!;
            var selected = actor?.Invoke(person) ?? data;
            return Task.FromResult(HttpResponses.Json(JsonSerializer.Serialize(new { actor = new { id = selected.ActorId } })));
        });
    }

    internal static void AssertRequests(GatewayApplicationFactory host, ExternalValues connection,
        IReadOnlyCollection<ExternalValues> actors, IReadOnlyCollection<ExternalValues>? allowedActors = null)
    {
        var requests = host.Connections.Requests.ToArray();
        Assert.Contains(requests, sent => sent.Uri == host.GetUri);
        foreach (var actor in actors)
            Assert.Contains(requests, sent => sent.Uri == host.ActorUri && ExternalId(sent) == actor.PersonId);
        foreach (var sent in requests)
        {
            Assert.Equal(HttpMethod.Post, sent.Method);
            Assert.Equal("application/json",
                MediaTypeHeaderValue.Parse(Assert.Single(sent.ContentHeaders["Content-Type"])).MediaType);
            using var json = sent.Json();
            var root = json.RootElement;
            Assert.Equal(JsonValueKind.Object, root.ValueKind);
            if (sent.Uri == host.GetUri)
            {
                var value = root.GetProperty("connection");
                Assert.Equal(JsonValueKind.Object, value.ValueKind);
                Assert.Equal(connection.ConnectionId, value.GetProperty("id").GetString());
            }
            else
            {
                Assert.Equal(host.ActorUri, sent.Uri);
                Assert.Equal("line", root.GetProperty("channel_type").GetString());
                Assert.Contains(allowedActors ?? actors, actor => actor.PersonId == root.GetProperty("external_id").GetString());
            }
        }
        Assert.Empty(host.Unexpected.Requests);
    }

    private static string? ExternalId(CapturedHttp sent)
    {
        using var json = sent.Json();
        return json.RootElement.GetProperty("external_id").GetString();
    }
}
#endif
