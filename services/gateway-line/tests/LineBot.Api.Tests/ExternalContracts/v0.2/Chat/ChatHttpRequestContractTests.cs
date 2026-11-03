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
public sealed class ChatHttpRequestContractTests : ExternalFlowTest
{
    public ChatHttpRequestContractTests(ITestOutputHelper output) : base(output) { }
    [Fact(DisplayName = "C02-External-01: Webhook input becomes the v0.2 Chat request")]
    [Trait("Case", "C02-External-01")]
    public async Task Webhook_TextEvent_SendsV02ChatRequest()
    {
        // Arrange
        var data = ExternalSamples.Random(LineWebhookSamples.RichText);
        using var chat = new CaptureHandler(async (sent, _) => {
            var response = ChatV02Samples.Ok(sent, data.OutputMessageId, data.ReplyText);
            Trace("C02-External-01-01-chat-response", new { StatusCode = (int)response.StatusCode,
                Body = await response.Content.ReadAsStringAsync() });
            return response;
        });
        using var line = new CaptureHandler((received, _) => {
            Trace("C02-External-01-01-line-request", received);
            return Task.FromResult(LineReplySamples.SuccessResponse());
        });
        using var host = new GatewayApplicationFactory(chat, line);
        host.Settings["Character:Id"] = data.CharacterId;
        host.Settings["Chat:RequestTimeoutSeconds"] = "5";
        var body = data.Body();
        Record("C02-External-01-01", host, data, body, LineWebhookSamples.Sign(body),
            new { ChatStatus = 200, Messages = new[] { data.ReplyText }, LineStatus = 200 });
        using var client = host.CreateClient();
        using var request = LineWebhookSamples.Incoming(body, LineWebhookSamples.Sign(body));
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // Act
        using var webhookResponse = await client.SendAsync(request, deadline.Token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, webhookResponse.StatusCode);
        deadline.Dispose();
        var observed = await host.Chat.NextAsync();
        Trace("C02-External-01-01-observed", new { Chat = observed, Line = line.Requests });
        ChatV02Assertions.AssertRequest(observed, data, "https://chat.test/chat/v0.2/generate-replies");
        LineReplyAssertions.AssertRequest(await host.Line.NextAsync(), data, data.ReplyText);
        await Task.WhenAll(chat.AssertNoPendingRequestsAsync(), line.AssertNoPendingRequestsAsync());
        await host.StopHostAsync();
        Assert.Single(chat.Requests);
        Assert.Single(line.Requests);
    }

    [Fact(DisplayName = "C02-External-03: Separate events receive fresh input IDs")]
    [Trait("Case", "C02-External-03")]
    public async Task Webhook_SeparateEvents_ReceiveDifferentIds()
    {
        var first = ExternalSamples.Random();
        var second = ExternalSamples.Random() with { PersonId = first.PersonId,
            Destination = first.Destination, CharacterId = first.CharacterId, Text = first.Text };
        var ids = new List<(string Interaction, string Message)>();
        Trace("C02-External-03-01", new { First = first, Second = second, ExpectedChatCountPerHost = 1, ExpectedLineCountPerHost = 1 });
        foreach (var data in new[] { first, second }) {
            using var chat = new CaptureHandler(async (sent, _) => {
                var response = ChatV02Samples.Ok(sent, data.OutputMessageId, data.ReplyText);
                Trace("C02-External-03-01-chat-response", new { StatusCode = (int)response.StatusCode,
                    Body = await response.Content.ReadAsStringAsync() });
                return response;
            });
            using var line = new CaptureHandler((_, _) => Task.FromResult(LineReplySamples.SuccessResponse()));
            using var host = new GatewayApplicationFactory(chat, line);
            host.Settings["Character:Id"] = data.CharacterId;
            var body = data.Body();
            Record("C02-External-03-01", host, data, body, LineWebhookSamples.Sign(body),
                new { ChatStatus = 200, LineStatus = 200, ExpectedChatCount = 1, ExpectedLineCount = 1 });
            using var client = host.CreateClient();
            using var request = LineWebhookSamples.Incoming(body, LineWebhookSamples.Sign(body));
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var response = await client.SendAsync(request, deadline.Token);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var sent = await chat.NextAsync();
            ChatV02Assertions.AssertRequest(sent, data, "https://chat.test/chat/v0.2/generate-replies");
            using var json = sent.Json();
            ids.Add((json.RootElement.GetProperty("interaction_id").GetString()!,
                json.RootElement.GetProperty("message").GetProperty("id").GetString()!));
            LineReplyAssertions.AssertRequest(await line.NextAsync(), data, data.ReplyText);
            Trace("C02-External-03-01-observed", new { Chat = sent, Line = line.Requests, Ids = ids.ToArray() });
            await Task.WhenAll(chat.AssertNoPendingRequestsAsync(), line.AssertNoPendingRequestsAsync());
            await host.StopHostAsync();
            Assert.Single(chat.Requests); Assert.Single(line.Requests);
        }
        Assert.NotEqual(ids[0].Interaction, ids[1].Interaction);
        Assert.NotEqual(ids[0].Message, ids[1].Message);
    }
}
