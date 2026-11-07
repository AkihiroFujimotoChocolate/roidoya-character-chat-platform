// SPDX-License-Identifier: MIT
using System.Net;
using LineBot.Api.Tests.Support;
using LineBot.Api.Tests.ExternalContracts.V01.Support;
using LineBot.Api.Tests.ExternalContracts.V01.Line;
using Xunit.Abstractions;
namespace LineBot.Api.Tests.ExternalContracts.V01.Chat;

public sealed class ChatRoutingContractTests : ExternalFlowTest
{
    public ChatRoutingContractTests(ITestOutputHelper output) : base(output) { }

    [Theory(DisplayName = "External-06: Configured endpoint takes precedence over the endpoint template")]
    [Trait("Case", "External-06")]
    [InlineData("External-06-01", "The existing API endpoint overrides the endpoint template", "/api/chat/v0.1")]
    [InlineData("External-06-02", "The smoke-test endpoint overrides the endpoint template", "/chat/v0.1/generate-replies")]
    public async Task Webhook_ConfiguredEndpoint_OverridesEndpointTemplate(string caseId, string caseDescription, string endpoint)
    {
        // Arrange
        var data = ExternalSamples.Random();
        using var chat = new CaptureHandler((_, _) => Task.FromResult(ChatV01Samples.Ok(data.ReplyText)));
        using var line = new CaptureHandler((_, _) => Task.FromResult(LineReplySamples.SuccessResponse()));
        using var host = new GatewayApplicationFactory(chat, line);
        host.Settings["Chat:Http:Endpoint"] = endpoint;
        host.Settings["Chat:Http:EndpointTemplate"] = "/unused/{version}/reply";
        var body = data.Body();
        Record(caseId,
            host, data, body, LineWebhookSamples.Sign(body),
            new { ChatStatus = 200, Messages = new[] { data.ReplyText }, LineStatus = 200 });
        using var client = host.CreateClient();
        using var request = LineWebhookSamples.Incoming(body, LineWebhookSamples.Sign(body));
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // Act
        using var webhookResponse = await client.SendAsync(request, deadline.Token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, webhookResponse.StatusCode);
        deadline.Dispose();
        ChatV01Assertions.AssertRequest(await host.Chat.NextAsync(), data, "https://chat.test" + endpoint);
        LineReplyAssertions.AssertRequest(await host.Line.NextAsync(), data, data.ReplyText);
        await Task.WhenAll(chat.AssertNoPendingRequestsAsync(), line.AssertNoPendingRequestsAsync());
        await host.StopHostAsync();
        Assert.Single(chat.Requests);
        Assert.Single(line.Requests);
    }
}

