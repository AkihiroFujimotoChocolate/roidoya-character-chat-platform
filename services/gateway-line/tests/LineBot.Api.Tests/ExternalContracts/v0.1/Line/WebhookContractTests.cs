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
namespace LineBot.Api.Tests.ExternalContracts.V01.Line;
public sealed class WebhookContractTests : ExternalFlowTest
{
    public WebhookContractTests(ITestOutputHelper output) : base(output) { }
    [Theory(DisplayName = "C01-External-05: Invalid signatures return 400 without Chat or LINE requests")]
    [Trait("Case", "C01-External-28")]
    [InlineData("C01-External-28-01", "A missing signature returns 400 without Chat or LINE requests", "missing")]
    [InlineData("C01-External-28-02", "A signature from a different secret returns 400 without Chat or LINE requests", "wrong-secret")]
    [InlineData("C01-External-28-03", "Changing the valid JSON body after signing returns 400 without Chat or LINE requests", "changed-body")]
    public async Task Webhook_InvalidSignature_ReturnsBadRequestWithoutOutboundRequests(string caseId, string caseDescription, string failure)
    {
        // Arrange
        var data = ExternalSamples.Random();
        using var chat = new CaptureHandler((sent, _) => Task.FromResult(ChatV01Samples.Ok(data.ReplyText)));
        using var line = new CaptureHandler((_, _) => Task.FromResult(LineReplySamples.SuccessResponse()));
        using var connections = ConnectionSamples.Capture(data);
        using var host = new GatewayApplicationFactory(chat, line, connections);
        var original = data.Body();
        var sent = failure == "changed-body" ? original.Concat(Encoding.UTF8.GetBytes(" ")).ToArray() : original;
        var signature = failure == "missing" ? null : LineWebhookSamples.Sign(original,
            failure == "wrong-secret" ? "another-synthetic-secret" : LineWebhookSamples.Secret);
        Record(caseId,
            host, data, sent, signature, new { Failure = failure,
                OriginalBodyBase64 = Convert.ToBase64String(original), ExpectedStatus = 400, AlternateSecret = "another-synthetic-secret", ExpectedChatCount = 0, ExpectedLineCount = 0 });
        using var client = host.CreateClient();
        using var request = LineWebhookSamples.Incoming(sent, signature, "/line/webhook");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        // Act
        using var response = await client.SendAsync(request, deadline.Token);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Trace(caseId + "-observed", new { Chat = chat.Requests, Line = line.Requests });
        await Task.WhenAll(chat.AssertNoPendingRequestsAsync(), line.AssertNoPendingRequestsAsync());
        await host.StopHostAsync();
        Assert.Empty(chat.Requests);
        Assert.Empty(line.Requests);
        ConnectionSamples.AssertRequests(host, data, Array.Empty<ExternalValues>());
    }

    [Theory(DisplayName = "C01-External-04: Excluded input produces no requests after a successful control")]
    [Trait("Case", "C01-External-29")]
    [InlineData("C01-External-29-01", "Standby text is ignored", "standby", "message", "text", null, null)]
    [InlineData("C01-External-29-02", "Follow without a message is ignored", "active", "follow", "text", null, null)]
    [InlineData("C01-External-29-03", "Image without text is ignored", "active", "message", "image", null, null)]
    [InlineData("C01-External-29-04", "Empty text is ignored", "active", "message", "text", "", null)]
    [InlineData("C01-External-29-05", "Whitespace text is ignored", "active", "message", "text", " \t\n", null)]
    [InlineData("C01-External-29-06", "Empty reply token is ignored", "active", "message", "text", null, "")]
    [InlineData("C01-External-29-07", "Whitespace reply token is ignored", "active", "message", "text", null, " \t\n")]
    public async Task Webhook_OutOfScopeEvent_ReturnsOkWithoutOutboundRequests(
        string caseId, string caseDescription, string mode, string eventType, string messageType,
        string? text, string? replyToken)
    {
        var control = ExternalSamples.Random();
        var data = ExternalSamples.Random() with { CharacterId = control.CharacterId, ConnectionId = control.ConnectionId, Destination = control.Destination };
        data = data with { Text = text ?? data.Text, ReplyToken = replyToken ?? data.ReplyToken };
        using var chat = new CaptureHandler(async (sent, _) => {
            var response = ChatV01Samples.Ok(control.ReplyText);
            Trace(caseId + "-chat-response", new { StatusCode = (int)response.StatusCode,
                Body = await response.Content.ReadAsStringAsync() });
            return response;
        });
        using var line = new CaptureHandler((_, _) => Task.FromResult(LineReplySamples.SuccessResponse()));
        using var connections = ConnectionSamples.Capture(control, person => person == control.PersonId ? control : data);
        using var host = new GatewayApplicationFactory(chat, line, connections);
        var controlBody = control.Body();
        var body = data.Body(mode: mode, kind: eventType, messageKind: messageType);
        Record(caseId, host, control, controlBody, LineWebhookSamples.Sign(controlBody),
            new { Role = "control", ChatStatus = 200, LineStatus = 200, Text = control.ReplyText });
        Record(caseId, host, data, body, LineWebhookSamples.Sign(body),
            new { Role = "excluded", ExpectedStatus = 200, AdditionalChat = 0, AdditionalLine = 0, TotalChat = 1, TotalLine = 1 },
            chatResponse: control, connection: control);
        using var client = host.CreateClient();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var normal = LineWebhookSamples.Incoming(controlBody, LineWebhookSamples.Sign(controlBody), "/line/webhook");
        using var normalResponse = await client.SendAsync(normal, deadline.Token);
        Assert.Equal(HttpStatusCode.OK, normalResponse.StatusCode);
        ChatV01Assertions.AssertRequest(await chat.NextAsync(), control, "https://chat.test/chat/v0.1/generate-replies");
        LineReplyAssertions.AssertRequest(await line.NextAsync(), control, control.ReplyText);
        using var request = LineWebhookSamples.Incoming(body, LineWebhookSamples.Sign(body), "/line/webhook");
        using var inputLimit = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var response = await client.SendAsync(request, inputLimit.Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await Task.WhenAll(chat.AssertNoPendingRequestsAsync(), line.AssertNoPendingRequestsAsync());
        await host.StopHostAsync();
        Assert.Single(chat.Requests); Assert.Single(line.Requests);
        ConnectionSamples.AssertRequests(host, control, new[] { control }, new[] { control, data });
        Trace(caseId + "-observed", new { Chat = chat.Requests, Line = line.Requests });
    }
}

#else
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
        Assert.Single(chat.Requests);
        Assert.Single(line.Requests);
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


#endif
