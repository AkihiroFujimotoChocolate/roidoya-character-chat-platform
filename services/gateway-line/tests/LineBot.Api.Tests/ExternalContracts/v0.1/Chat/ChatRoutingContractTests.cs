#if PHASE2_GATEWAY_TESTS
// SPDX-License-Identifier: MIT
using LineBot.Api.Tests.ExternalContracts.V01.ConnectionManagement;
using System.Net;
using System.Text;
using System.Text.Json;
using LineBot.Api.Tests.Support;
using LineBot.Api.Tests.ExternalContracts.V01.Support;
using LineBot.Api.Tests.ExternalContracts.V01.Chat;
using LineBot.Api.Tests.ExternalContracts.V01.Line;
using LineReplyAssertions = LineBot.Api.Tests.ExternalContracts.V01.Line.LineReplyAssertions;
using Xunit.Abstractions;
namespace LineBot.Api.Tests.ExternalContracts.V01.Chat;
public sealed class ChatRoutingContractTests : ExternalFlowTest
{
    public ChatRoutingContractTests(ITestOutputHelper output) : base(output) { }
    [Theory(DisplayName = "C01-External-06: Configured endpoint takes precedence over the endpoint template")]
    [Trait("Case", "C01-External-06")]
    [InlineData("C01-External-06-01", "Explicit endpoint takes priority", "/api/chat/v0.1", "/unused/{version}/reply", "https://chat.test/", "https://app.test/", "https://chat.test/api/chat/v0.1")]
    [InlineData("C01-External-06-02", "Template expands the selected version", null, "/custom/{version}/reply", "https://chat.test/", "https://app.test/", "https://chat.test/custom/v0.1/reply")]
    [InlineData("C01-External-06-03", "Absent routes use the version default", null, null, "https://chat.test/", "https://app.test/", "https://chat.test/chat/v0.1/generate-replies")]
    [InlineData("C01-External-06-04", "Absolute endpoint is used unchanged", "https://absolute.test/selected", "/unused/{version}/reply", "https://chat.test/", "https://app.test/", "https://absolute.test/selected")]
    [InlineData("C01-External-06-05", "Absent Chat base uses App base", "/selected", null, null, "https://app.test/", "https://app.test/selected")]
    public async Task Webhook_ConfiguredEndpoint_OverridesEndpointTemplate(string caseId, string caseDescription, string? endpoint, string? template, string? chatBase, string appBase, string expected)
    {
        // Arrange
        var data = ExternalSamples.Random();
        using var chat = new CaptureHandler(async (sent, _) => {
            var response = ChatV01Samples.Ok(data.ReplyText);
            Trace(caseId + "-chat-response", new { StatusCode = (int)response.StatusCode,
                Body = await response.Content.ReadAsStringAsync() });
            return response;
        });
        using var line = new CaptureHandler((_, _) => Task.FromResult(LineReplySamples.SuccessResponse()));
        using var connections = ConnectionSamples.Capture(data);
        using var host = new GatewayApplicationFactory(chat, line, connections) { ChatUri = new Uri(expected) };
        host.Settings.Remove("Chat:Http:Endpoint");
        host.Settings.Remove("Chat:Http:BaseUrl");
        if (endpoint is not null) host.Settings["Chat:Http:Endpoint"] = endpoint;
        if (template is not null) host.Settings["Chat:Http:EndpointTemplate"] = template;
        if (chatBase is not null) host.Settings["Chat:Http:BaseUrl"] = chatBase;
        host.Settings["App:BaseUrl"] = appBase;
        var body = data.Body();
        Record(caseId,
            host, data, body, LineWebhookSamples.Sign(body),
            new { Description = caseDescription, ChatStatus = 200, Messages = new[] { data.ReplyText }, LineStatus = 200, ExpectedUrl = expected, ExpectedChatCount = 1, ExpectedLineCount = 1 });
        using var client = host.CreateClient();
        using var request = LineWebhookSamples.Incoming(body, LineWebhookSamples.Sign(body), "/line/webhook");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // Act
        using var webhookResponse = await client.SendAsync(request, deadline.Token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, webhookResponse.StatusCode);
        deadline.Dispose();
        ChatV01Assertions.AssertRequest(await host.Chat.NextAsync(), data, expected);
        LineReplyAssertions.AssertRequest(await host.Line.NextAsync(), data, data.ReplyText);
        Trace(caseId + "-observed", new { Chat = chat.Requests, Line = line.Requests });
        await Task.WhenAll(chat.AssertNoPendingRequestsAsync(), line.AssertNoPendingRequestsAsync());
        await host.StopHostAsync();
        Assert.Single(chat.Requests);
        Assert.Single(line.Requests);
        ConnectionSamples.AssertRequests(host, data, new[] { data });
    }
}

#else
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


#endif
