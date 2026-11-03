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
namespace LineBot.Api.Tests.ExternalContracts.V02.Line;
public sealed class WebhookContractTests : ExternalFlowTest
{
    public WebhookContractTests(ITestOutputHelper output) : base(output) { }
    [Theory(DisplayName = "C02-External-05: Invalid signatures return 400 without outbound requests")]
    [Trait("Case", "C02-External-05")]
    [InlineData("C02-External-05-01", "A missing signature returns 400 without Chat or LINE requests", "missing")]
    [InlineData("C02-External-05-02", "A signature from a different secret returns 400 without Chat or LINE requests", "wrong-secret")]
    [InlineData("C02-External-05-03", "Changing the valid JSON body after signing returns 400 without Chat or LINE requests", "changed-body")]
    public async Task Webhook_InvalidSignature_ReturnsBadRequestWithoutOutboundRequests(string caseId, string caseDescription, string failure)
    {
        // Arrange
        var data = ExternalSamples.Random(LineWebhookSamples.RichText) with { ReplyText = "reply-a" };
        using var chat = new CaptureHandler((sent, _) => Task.FromResult(ChatV02Samples.Ok(sent, data.OutputMessageId, "reply-a")));
        using var line = new CaptureHandler((_, _) => Task.FromResult(LineReplySamples.SuccessResponse()));
        using var host = new GatewayApplicationFactory(chat, line);
        host.Settings["Character:Id"] = data.CharacterId;
        var original = data.Body();
        var sent = failure == "changed-body" ? original.Concat(Encoding.UTF8.GetBytes(" ")).ToArray() : original;
        var signature = failure == "missing" ? null : LineWebhookSamples.Sign(original,
            failure == "wrong-secret" ? "another-synthetic-secret" : LineWebhookSamples.Secret);
        Record(caseId,
            host, data, sent, signature, new { Failure = failure,
                OriginalBodyBase64 = Convert.ToBase64String(original), ExpectedStatus = 400, AlternateSecret = "another-synthetic-secret", ExpectedChatCount = 0, ExpectedLineCount = 0 });
        using var client = host.CreateClient();
        using var request = LineWebhookSamples.Incoming(sent, signature);
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
    }

    [Theory(DisplayName = "C02-External-04: Excluded input produces no requests after a successful control")]
    [Trait("Case", "C02-External-04")]
    [InlineData("C02-External-04-01", "Standby text is ignored", "standby", "message", "text", null, null)]
    [InlineData("C02-External-04-02", "Follow without a message is ignored", "active", "follow", "text", null, null)]
    [InlineData("C02-External-04-03", "Image without text is ignored", "active", "message", "image", null, null)]
    [InlineData("C02-External-04-04", "Empty text is ignored", "active", "message", "text", "", null)]
    [InlineData("C02-External-04-05", "Whitespace text is ignored", "active", "message", "text", " \t\n", null)]
    [InlineData("C02-External-04-06", "Empty reply token is ignored", "active", "message", "text", null, "")]
    [InlineData("C02-External-04-07", "Whitespace reply token is ignored", "active", "message", "text", null, " \t\n")]
    public async Task Webhook_OutOfScopeEvent_ReturnsOkWithoutOutboundRequests(
        string caseId, string caseDescription, string mode, string eventType, string messageType,
        string? text, string? replyToken)
    {
        var control = ExternalSamples.Random() with { ReplyText = "reply-a" };
        var data = ExternalSamples.Random() with { CharacterId = control.CharacterId, ReplyText = "reply-a" };
        data = data with { Text = text ?? data.Text, ReplyToken = replyToken ?? data.ReplyToken };
        using var chat = new CaptureHandler(async (sent, _) => {
            var response = ChatV02Samples.Ok(sent, control.OutputMessageId, "reply-a");
            Trace(caseId + "-chat-response", new { StatusCode = (int)response.StatusCode,
                Body = await response.Content.ReadAsStringAsync() });
            return response;
        });
        using var line = new CaptureHandler((_, _) => Task.FromResult(LineReplySamples.SuccessResponse()));
        using var host = new GatewayApplicationFactory(chat, line);
        host.Settings["Character:Id"] = control.CharacterId;
        var controlBody = control.Body();
        var body = data.Body(mode: mode, kind: eventType, messageKind: messageType);
        Record(caseId, host, control, controlBody, LineWebhookSamples.Sign(controlBody),
            new { Role = "control", ChatStatus = 200, LineStatus = 200, Text = "reply-a" });
        Record(caseId, host, data, body, LineWebhookSamples.Sign(body),
            new { Role = "excluded", ExpectedStatus = 200, AdditionalChat = 0, AdditionalLine = 0, TotalChat = 1, TotalLine = 1 },
            responseOutputId: control.OutputMessageId);
        using var client = host.CreateClient();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var normal = LineWebhookSamples.Incoming(controlBody, LineWebhookSamples.Sign(controlBody));
        using var normalResponse = await client.SendAsync(normal, deadline.Token);
        Assert.Equal(HttpStatusCode.OK, normalResponse.StatusCode);
        ChatV02Assertions.AssertRequest(await chat.NextAsync(), control, "https://chat.test/chat/v0.2/generate-replies");
        LineReplyAssertions.AssertRequest(await line.NextAsync(), control, "reply-a");
        using var request = LineWebhookSamples.Incoming(body, LineWebhookSamples.Sign(body));
        using var inputLimit = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var response = await client.SendAsync(request, inputLimit.Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await Task.WhenAll(chat.AssertNoPendingRequestsAsync(), line.AssertNoPendingRequestsAsync());
        await host.StopHostAsync();
        Assert.Single(chat.Requests); Assert.Single(line.Requests);
        Trace(caseId + "-observed", new { Chat = chat.Requests, Line = line.Requests });
    }
}
