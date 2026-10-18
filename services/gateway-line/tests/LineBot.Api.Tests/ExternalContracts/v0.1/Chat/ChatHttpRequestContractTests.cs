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
        Assert.Equal(1, chat.Requests.Count);
        Assert.Equal(1, line.Requests.Count);
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
        Assert.Equal(1, chat.Requests.Count);
        Assert.Equal(1, line.Requests.Count);
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
        Assert.Equal(1, chat.Requests.Count);
        Assert.Equal(1, line.Requests.Count);
    }
}

