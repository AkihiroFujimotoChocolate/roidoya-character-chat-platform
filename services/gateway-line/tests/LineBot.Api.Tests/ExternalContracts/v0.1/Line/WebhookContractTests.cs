// SPDX-License-Identifier: MIT
using System.Net;
using System.Text;
using LineBot.Api.Tests.Support;
using LineBot.Api.Tests.ExternalContracts.V01.Support;
using LineBot.Api.Tests.ExternalContracts.V01.Chat;
using Xunit.Abstractions;
namespace LineBot.Api.Tests.ExternalContracts.V01.Line;

public sealed class WebhookContractTests : ExternalFlowTest
{
    public WebhookContractTests(ITestOutputHelper output) : base(output) { }

    [Theory(DisplayName = "External-27: A signed text event reaches Chat and LINE over HTTP")]
    [Trait("Case", "External-27")]
    [InlineData("External-27-01", "Initial delivery sends the matching Chat request and LINE reply", false)]
    [InlineData("External-27-02", "Redelivery of an unprocessed event sends the matching Chat request and LINE reply", true)]
    public async Task Webhook_SignedTextEvent_SendsChatAndLineRequests(string caseId, string caseDescription, bool redelivery)
    {
        // Arrange
        var data = ExternalSamples.Random(LineWebhookSamples.RichText);
        using var chat = new CaptureHandler((_, _) => Task.FromResult(ChatV01Samples.Ok(data.ReplyText)));
        using var line = new CaptureHandler((_, _) => Task.FromResult(LineReplySamples.SuccessResponse()));
        using var host = new GatewayApplicationFactory(chat, line);
        var body = data.Body(redelivery);
        Record(caseId, host, data, body, LineWebhookSamples.Sign(body),
            new { Redelivery = redelivery, ChatStatus = 200, Messages = new[] { data.ReplyText }, LineStatus = 200 });
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
        Assert.Equal(1, chat.Requests.Count);
        Assert.Equal(1, line.Requests.Count);
    }

    [Theory(DisplayName = "External-28: Invalid signatures return 400 without outbound requests")]
    [Trait("Case", "External-28")]
    [InlineData("External-28-01", "A missing signature returns 400 without Chat or LINE requests", "missing")]
    [InlineData("External-28-02", "A signature from a different secret returns 400 without Chat or LINE requests", "wrong-secret")]
    [InlineData("External-28-03", "Changing the valid JSON body after signing returns 400 without Chat or LINE requests", "changed-body")]
    public async Task Webhook_InvalidSignature_ReturnsBadRequestWithoutOutboundRequests(string caseId, string caseDescription, string failure)
    {
        // Arrange
        var data = ExternalSamples.Random(LineWebhookSamples.RichText);
        using var chat = new CaptureHandler((_, _) => Task.FromResult(ChatV01Samples.Ok("reply-a")));
        using var line = new CaptureHandler((_, _) => Task.FromResult(LineReplySamples.SuccessResponse()));
        using var host = new GatewayApplicationFactory(chat, line);
        var original = data.Body();
        var sent = failure == "changed-body" ? original.Concat(Encoding.UTF8.GetBytes(" ")).ToArray() : original;
        var signature = failure == "missing" ? null : LineWebhookSamples.Sign(original,
            failure == "wrong-secret" ? "another-synthetic-secret" : LineWebhookSamples.Secret);
        Record(caseId,
            host, data, sent, signature, new { Failure = failure,
                OriginalBodyBase64 = Convert.ToBase64String(original), ExpectedStatus = 400 });
        using var client = host.CreateClient();
        using var request = LineWebhookSamples.Incoming(sent, signature);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        // Act
        using var response = await client.SendAsync(request, deadline.Token);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await Task.WhenAll(chat.AssertNoPendingRequestsAsync(), line.AssertNoPendingRequestsAsync());
        await host.StopHostAsync();
        Assert.Empty(chat.Requests);
        Assert.Empty(line.Requests);
    }

    [Theory(DisplayName = "External-29: Out-of-scope events return 200 without outbound requests")]
    [Trait("Case", "External-29")]
    [InlineData("External-29-01", "A standby text message returns 200 without Chat or LINE requests", "standby", "standby", "message", "text")]
    [InlineData("External-29-02", "A follow event returns 200 without Chat or LINE requests", "follow", "active", "follow", "text")]
    [InlineData("External-29-03", "An image message returns 200 without Chat or LINE requests", "image", "active", "message", "image")]
    public async Task Webhook_OutOfScopeEvent_ReturnsOkWithoutOutboundRequests(string caseId, string caseDescription, string kind,
        string mode, string eventType, string messageType)
    {
        // Arrange
        var data = ExternalSamples.Random();
        using var chat = new CaptureHandler((_, _) => Task.FromResult(ChatV01Samples.Ok("reply-a")));
        using var line = new CaptureHandler((_, _) => Task.FromResult(LineReplySamples.SuccessResponse()));
        using var host = new GatewayApplicationFactory(chat, line);
        var body = data.Body(mode: mode, kind: eventType, messageKind: messageType);
        Record(caseId,
            host, data, body, LineWebhookSamples.Sign(body), new { Kind = kind, ExpectedStatus = 200 });
        using var client = host.CreateClient();
        using var request = LineWebhookSamples.Incoming(body, LineWebhookSamples.Sign(body));
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // Act
        using var webhookResponse = await client.SendAsync(request, deadline.Token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, webhookResponse.StatusCode);
        deadline.Dispose();
        await Task.WhenAll(chat.AssertNoPendingRequestsAsync(), line.AssertNoPendingRequestsAsync());
        await host.StopHostAsync();
        Assert.Empty(chat.Requests);
        Assert.Empty(line.Requests);
    }
}

