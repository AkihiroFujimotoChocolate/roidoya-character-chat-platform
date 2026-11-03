// SPDX-License-Identifier: MIT
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
namespace LineBot.Api.Tests.ExternalContracts.V02.Chat;
public sealed class ChatRoutingContractTests : ExternalFlowTest
{
    public ChatRoutingContractTests(ITestOutputHelper output) : base(output) { }
    [Theory(DisplayName = "C02-External-02: Configured endpoint takes precedence over the endpoint template")]
    [Trait("Case", "C02-External-02")]
            [InlineData("C02-External-02-01", "Explicit endpoint takes priority", "/selected", "/unused/{version}/reply", "https://chat.test/", "https://app.test/", "https://chat.test/selected")]
    [InlineData("C02-External-02-02", "Template expands the selected version", "", "/custom/{version}/reply", "https://chat.test/", "https://app.test/", "https://chat.test/custom/v0.2/reply")]
    [InlineData("C02-External-02-03", "Empty routes use the version default", "", "", "https://chat.test/", "https://app.test/", "https://chat.test/chat/v0.2/generate-replies")]
    [InlineData("C02-External-02-04", "Absolute endpoint is used unchanged", "https://absolute.test/selected", "/unused/{version}/reply", "https://chat.test/", "https://app.test/", "https://absolute.test/selected")]
    [InlineData("C02-External-02-05", "Empty Chat base uses App base", "/selected", "", "", "https://app.test/", "https://app.test/selected")]
    public async Task Webhook_ConfiguredEndpoint_OverridesEndpointTemplate(string caseId, string caseDescription, string endpoint, string template, string chatBase, string appBase, string expected)
    {
        // Arrange
        var data = ExternalSamples.Random();
        using var chat = new CaptureHandler((sent, _) => Task.FromResult(ChatV02Samples.Ok(sent, data.OutputMessageId, data.ReplyText)));
        using var line = new CaptureHandler((_, _) => Task.FromResult(LineReplySamples.SuccessResponse()));
        using var host = new GatewayApplicationFactory(chat, line);
        host.Settings["Chat:Http:Endpoint"] = endpoint;
        host.Settings["Chat:Http:EndpointTemplate"] = template;
        host.Settings["Character:Id"] = data.CharacterId;
        host.Settings["Chat:Http:BaseUrl"] = chatBase;
        host.Settings["App:BaseUrl"] = appBase;
        var body = data.Body();
        Record(caseId,
            host, data, body, LineWebhookSamples.Sign(body),
            new { ChatStatus = 200, Messages = new[] { data.ReplyText }, LineStatus = 200, ExpectedUrl = expected, ExpectedChatCount = 1, ExpectedLineCount = 1 });
        using var client = host.CreateClient();
        using var request = LineWebhookSamples.Incoming(body, LineWebhookSamples.Sign(body));
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // Act
        using var webhookResponse = await client.SendAsync(request, deadline.Token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, webhookResponse.StatusCode);
        deadline.Dispose();
        ChatV02Assertions.AssertRequest(await host.Chat.NextAsync(), data, expected);
        LineReplyAssertions.AssertRequest(await host.Line.NextAsync(), data, data.ReplyText);
        Trace(caseId + "-observed", new { Chat = chat.Requests, Line = line.Requests });
        await Task.WhenAll(chat.AssertNoPendingRequestsAsync(), line.AssertNoPendingRequestsAsync());
        await host.StopHostAsync();
        Assert.Single(chat.Requests);
        Assert.Single(line.Requests);
    }
}
