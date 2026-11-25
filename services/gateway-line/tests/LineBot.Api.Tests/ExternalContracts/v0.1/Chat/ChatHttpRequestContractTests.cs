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
public sealed class ChatHttpRequestContractTests : ExternalFlowTest
{
    public ChatHttpRequestContractTests(ITestOutputHelper output) : base(output) { }
    [Theory(DisplayName = "C01-External-01: Webhook input becomes the v0.1 Chat request")]
    [Trait("Case", "C01-External-01")]
    [InlineData("C01-External-01-01", "Default author prefix", false)]
    [InlineData("C01-External-02-01", "Version and content type headers", false)]
    [InlineData("C01-External-14-01", "Rich signed text is preserved", false)]
    [InlineData("C01-External-27-01", "Signed direct event uses fixed connection", false)]
    [InlineData("C01-External-01-02", "Configured author prefix", true)]
    public async Task Webhook_TextEvent_SendsV01ChatRequest(string caseId, string description, bool differentDestination)
    {
        // Arrange
        var data = ExternalSamples.Random(caseId == "C01-External-14-01" ? LineWebhookSamples.RichText : null);
        var destination = differentDestination ? GeneratedValues.Id("line-destination") : data.Destination;
        using var chat = new CaptureHandler(async (sent, _) => {
            var response = ChatV01Samples.Ok(data.ReplyText);
            Trace(caseId + "-chat-response", new { StatusCode = (int)response.StatusCode,
                Body = await response.Content.ReadAsStringAsync() });
            return response;
        });
        using var line = new CaptureHandler((received, _) => {
            Trace(caseId + "-line-request", received);
            return Task.FromResult(LineReplySamples.SuccessResponse());
        });
        using var connections = ConnectionSamples.Capture(data, destination: destination);
        using var host = new GatewayApplicationFactory(chat, line, connections);
        host.Settings["Chat:RequestTimeoutSeconds"] = "5";
        if (differentDestination) host.Settings["Contracts:C0_1:Chat:AuthorUserIdPrefix"] = "author-test-";
        var body = data.Body();
        Record(caseId, host, data, body, LineWebhookSamples.Sign(body),
            new { Description = description, ChatStatus = 200, Messages = new[] { data.ReplyText }, LineStatus = 200 }, connectionDestination: destination);
        using var client = host.CreateClient();
        using var request = LineWebhookSamples.Incoming(body, LineWebhookSamples.Sign(body), "/line/webhook");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // Act
        using var webhookResponse = await client.SendAsync(request, deadline.Token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, webhookResponse.StatusCode);
        deadline.Dispose();
        var observed = await host.Chat.NextAsync();
        Trace(caseId + "-observed", new { Chat = observed, Line = line.Requests });
        ChatV01Assertions.AssertRequest(observed, data, "https://chat.test/chat/v0.1/generate-replies", differentDestination ? "author-test-" : "line-");
        LineReplyAssertions.AssertRequest(await host.Line.NextAsync(), data, data.ReplyText);
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

public sealed class ChatHttpRequestContractTests : ExternalFlowTest
{
    public ChatHttpRequestContractTests(ITestOutputHelper output) : base(output) { }

    [Fact(DisplayName = "External-01: Webhook input becomes the v0.1 Chat request")]
    [Trait("Case", "External-01")]
    public async Task Webhook_TextEvent_SendsV01ChatRequest()
    {
        // Arrange
        var data = ExternalSamples.Random();
        using var chat = new CaptureHandler((_, _) => Task.FromResult(ChatV01Samples.Ok(data.ReplyText)));
        using var line = new CaptureHandler((_, _) => Task.FromResult(LineReplySamples.SuccessResponse()));
        using var host = new GatewayApplicationFactory(chat, line);
        var body = data.Body();
        Record("External-01-01", host, data, body, LineWebhookSamples.Sign(body),
            new { ChatStatus = 200, Messages = new[] { data.ReplyText }, LineStatus = 200 });
        using var client = host.CreateClient();
        using var request = LineWebhookSamples.Incoming(body, LineWebhookSamples.Sign(body));
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // Act
        using var webhookResponse = await client.SendAsync(request, deadline.Token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, webhookResponse.StatusCode);
        deadline.Dispose();
        ChatV01Assertions.AssertRequest(await host.Chat.NextAsync(), data, "https://chat.test/chat/v0.1/generate-replies");
        LineReplyAssertions.AssertRequest(await host.Line.NextAsync(), data, data.ReplyText);
        await Task.WhenAll(chat.AssertNoPendingRequestsAsync(), line.AssertNoPendingRequestsAsync());
        await host.StopHostAsync();
        Assert.Single(chat.Requests);
        Assert.Single(line.Requests);
    }

    [Fact(DisplayName = "External-02: Default Chat headers omit Authorization when no key is configured")]
    [Trait("Case", "External-02")]
    public async Task Webhook_WithoutChatKey_SendsDefaultHeadersWithoutAuthorization()
    {
        // Arrange
        var data = ExternalSamples.Random();
        string? key = null;
        using var chat = new CaptureHandler((_, _) => Task.FromResult(ChatV01Samples.Ok(data.ReplyText)));
        using var line = new CaptureHandler((_, _) => Task.FromResult(LineReplySamples.SuccessResponse()));
        using var host = new GatewayApplicationFactory(chat, line);
        host.Settings["Chat:Http:ApiKey"] = key;
        var body = data.Body();
        Record("External-02-01", host, data, body, LineWebhookSamples.Sign(body),
            new { ChatKey = key, ChatStatus = 200, Messages = new[] { data.ReplyText }, LineStatus = 200 });
        using var client = host.CreateClient();
        using var request = LineWebhookSamples.Incoming(body, LineWebhookSamples.Sign(body));
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // Act
        using var webhookResponse = await client.SendAsync(request, deadline.Token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, webhookResponse.StatusCode);
        deadline.Dispose();
        var sent = await host.Chat.NextAsync();
        ChatV01Assertions.AssertRequest(sent, data, "https://chat.test/chat/v0.1/generate-replies");
        Assert.DoesNotContain(LineReplySamples.AccessToken, sent.Headers.Values.SelectMany(v => v));
        LineReplyAssertions.AssertRequest(await host.Line.NextAsync(), data, data.ReplyText);
        await Task.WhenAll(chat.AssertNoPendingRequestsAsync(), line.AssertNoPendingRequestsAsync());
        await host.StopHostAsync();
        Assert.Single(chat.Requests);
        Assert.Single(line.Requests);
    }

    [Fact(DisplayName = "External-14: Webhook special characters reach Chat unchanged")]
    [Trait("Case", "External-14")]
    public async Task Webhook_SpecialCharacters_PreservesChatText()
    {
        // Arrange
        var data = ExternalSamples.Random(LineWebhookSamples.RichText);
        using var chat = new CaptureHandler((_, _) => Task.FromResult(ChatV01Samples.Ok(data.ReplyText)));
        using var line = new CaptureHandler((_, _) => Task.FromResult(LineReplySamples.SuccessResponse()));
        using var host = new GatewayApplicationFactory(chat, line);
        var body = data.Body();
        Record("External-14-01", host, data, body, LineWebhookSamples.Sign(body),
            new { ChatStatus = 200, Messages = new[] { data.ReplyText }, LineStatus = 200 });
        using var client = host.CreateClient();
        using var request = LineWebhookSamples.Incoming(body, LineWebhookSamples.Sign(body));
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // Act
        using var webhookResponse = await client.SendAsync(request, deadline.Token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, webhookResponse.StatusCode);
        deadline.Dispose();
        ChatV01Assertions.AssertRequest(await host.Chat.NextAsync(), data, "https://chat.test/chat/v0.1/generate-replies");
        LineReplyAssertions.AssertRequest(await host.Line.NextAsync(), data, data.ReplyText);
        await Task.WhenAll(chat.AssertNoPendingRequestsAsync(), line.AssertNoPendingRequestsAsync());
        await host.StopHostAsync();
        Assert.Single(chat.Requests);
        Assert.Single(line.Requests);
    }
}


#endif
