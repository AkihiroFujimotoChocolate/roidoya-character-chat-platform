#if PHASE2_GATEWAY_TESTS
// SPDX-License-Identifier: MIT
using System.Net.Http.Headers;
using System.Text.Json;
using LineBot.Api.Tests.Support;
using LineBot.Api.Tests.ExternalContracts.V01.Line;
using LineBot.Api.Tests.ExternalContracts.V01.Support;
namespace LineBot.Api.Tests.ExternalContracts.V01.ConnectionManagement;
internal static class ConnectionSamples
{
    internal static object Connection(ExternalValues data, string? destination = null) => new
    {
        settings = new { channel_secret = LineWebhookSamples.Secret, channel_access_token = LineReplySamples.AccessToken }
    };
    internal static CaptureHandler Capture(ExternalValues data, Func<string, ExternalValues>? actor = null,
        string? destination = null, Uri? get = null) =>
        new((_, _) => Task.FromResult(HttpResponses.Json(JsonSerializer.Serialize(Connection(data)))));
    internal static void AssertRequests(GatewayApplicationFactory host, ExternalValues connection,
        IReadOnlyCollection<ExternalValues> actors, IReadOnlyCollection<ExternalValues>? allowedActors = null)
    {
        Assert.NotEmpty(host.Connections.Requests);
        foreach (var sent in host.Connections.Requests)
        {
            Assert.Equal(host.GetUri, sent.Uri);
            Assert.Equal(HttpMethod.Post, sent.Method);
            Assert.Equal("application/json", MediaTypeHeaderValue.Parse(Assert.Single(sent.ContentHeaders["Content-Type"])).MediaType);
            using var json = sent.Json();
            Assert.Equal(JsonValueKind.Object, json.RootElement.ValueKind);
            Assert.Empty(json.RootElement.EnumerateObject());
        }
        Assert.Empty(host.Unexpected.Requests);
    }
}
#endif
