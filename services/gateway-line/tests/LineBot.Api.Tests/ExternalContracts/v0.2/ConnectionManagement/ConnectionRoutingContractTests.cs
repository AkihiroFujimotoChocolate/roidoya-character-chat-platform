#if PHASE2_GATEWAY_TESTS
// SPDX-License-Identifier: MIT
using LineBot.Api.Tests.ExternalContracts.V02.ConnectionManagement;
using System.Net;
using System.Text;
using System.Text.Json;
using LineBot.Api.Tests.Support;
using LineBot.Api.Tests.ExternalContracts.V02.Support;
using LineBot.Api.Tests.ExternalContracts.V02.Chat;
using LineBot.Api.Tests.ExternalContracts.V02.Line;
using LineBot.Api.Tests.ExternalContracts.V01.Line;
using LineReplyAssertions = LineBot.Api.Tests.ExternalContracts.V02.Line.LineReplyAssertions;
using Xunit.Abstractions;
namespace LineBot.Api.Tests.ExternalContracts.V02.ConnectionManagement;
public sealed class ConnectionRoutingContractTests : ExternalFlowTest
{
    public ConnectionRoutingContractTests(ITestOutputHelper output) : base(output) { }
    [Theory(DisplayName = "C02-External-07: Connection Management routes honor endpoint settings")]
    [Trait("Case", "C02-External-07")]
    [InlineData("C02-External-07-01", "GetConnection: explicit endpoint", "GetConnection", "/chosen/get-connection", "/unused/{version}/get-connection", "https://connections.test/", "https://connections.test/chosen/get-connection")]
    [InlineData("C02-External-07-02", "GetConnection: version template", "GetConnection", null, "/chosen/{version}/get-connection", "https://connections.test/", "https://connections.test/chosen/v0.2/get-connection")]
    [InlineData("C02-External-07-03", "GetConnection: absolute endpoint", "GetConnection", "https://absolute.test/get-connection", null, "https://connections.test/", "https://absolute.test/get-connection")]
    [InlineData("C02-External-07-04", "GetConnection: App base fallback", "GetConnection", null, null, null, "https://app.test/channel/v0.2/get-connection")]
    [InlineData("C02-External-07-05", "ResolveActor: explicit endpoint", "ResolveActor", "/chosen/resolve-actor", "/unused/{version}/resolve-actor", "https://connections.test/", "https://connections.test/chosen/resolve-actor")]
    [InlineData("C02-External-07-06", "ResolveActor: version template", "ResolveActor", null, "/chosen/{version}/resolve-actor", "https://connections.test/", "https://connections.test/chosen/v0.2/resolve-actor")]
    [InlineData("C02-External-07-07", "ResolveActor: absolute endpoint", "ResolveActor", "https://absolute.test/resolve-actor", null, "https://connections.test/", "https://absolute.test/resolve-actor")]
    [InlineData("C02-External-07-08", "ResolveActor: App base fallback", "ResolveActor", null, null, null, "https://app.test/channel/v0.2/resolve-actor")]
    public async Task Webhook_ConfiguredConnectionEndpoint_RoutesRequest(string caseId, string caseDescription,
        string operation, string? endpoint, string? template, string? connectionBase, string expected)
    {
        // Arrange
        var data = ExternalSamples.Random();
        using var chat = new CaptureHandler(async (sent, _) => {
            var response = ChatV02Samples.AssertRequestAndCreateOk(sent, data.OutputMessageId, data.ReplyText);
            Trace(caseId + "-chat-response", new { StatusCode = (int)response.StatusCode,
                Body = await response.Content.ReadAsStringAsync() });
            return response;
        });
        using var line = new CaptureHandler((_, _) => Task.FromResult(LineReplySamples.SuccessResponse()));
        var otherBase = connectionBase ?? "https://app.test/";
        var get = new Uri(operation == "GetConnection" ? expected : otherBase + "channel/v0.2/get-connection");
        var actor = new Uri(operation == "ResolveActor" ? expected : otherBase + "channel/v0.2/resolve-actor");
        using var connections = ConnectionSamples.Capture(data, get: get);
        using var host = new GatewayApplicationFactory(chat, line, connections) { GetUri = get, ActorUri = actor };
        const string prefix = "ConnectionManagement:Http:";
        host.Settings.Remove(prefix + "BaseUrl");
        if (connectionBase is not null) host.Settings[prefix + "BaseUrl"] = connectionBase;
        if (endpoint is not null) host.Settings[prefix + operation + ":Endpoint"] = endpoint;
        if (template is not null) host.Settings[prefix + operation + ":EndpointTemplate"] = template;
        var body = data.Body();
        Record(caseId,
            host, data, body, LineWebhookSamples.Sign(body),
            new { Description = caseDescription, ChatStatus = 200, Messages = new[] { data.ReplyText }, LineStatus = 200, ExpectedUrl = expected, ExpectedChatCount = 1, ExpectedLineCount = 1 });
        using var client = host.CreateClient();
        using var request = LineWebhookSamples.Incoming(body, LineWebhookSamples.Sign(body), "/line/webhook/" + data.ConnectionId);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // Act
        using var webhookResponse = await client.SendAsync(request, deadline.Token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, webhookResponse.StatusCode);
        deadline.Dispose();
        ChatV02Assertions.AssertRequest(await host.Chat.NextAsync(), data, host.ChatUri.AbsoluteUri);
        LineReplyAssertions.AssertRequest(await host.Line.NextAsync(), data, data.ReplyText);
        Trace(caseId + "-observed", new { Chat = chat.Requests, Line = line.Requests });
        await Task.WhenAll(chat.AssertNoPendingRequestsAsync(), line.AssertNoPendingRequestsAsync());
        await host.StopHostAsync();
        Assert.Single(chat.Requests);
        Assert.Single(line.Requests);
        ConnectionSamples.AssertRequests(host, data, new[] { data });
    }
}
#endif
