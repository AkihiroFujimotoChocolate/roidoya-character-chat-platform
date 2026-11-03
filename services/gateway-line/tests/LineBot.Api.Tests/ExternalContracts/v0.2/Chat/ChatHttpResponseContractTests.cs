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
public sealed class ChatHttpResponseContractTests : ExternalFlowTest
{
    public ChatHttpResponseContractTests(ITestOutputHelper output) : base(output) { }
    [Fact(DisplayName = "C02-External-06: LINE reply waits for Chat and preserves its text")]
    [Trait("Case", "C02-External-06")]
    public async Task Webhook_PendingChat_WaitsThenPreservesReply()
    {
        var data = ExternalSamples.Random();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var chat = new CaptureHandler(async (sent, token) => {
            await release.Task.WaitAsync(token);
            var response = ChatV02Samples.Ok(sent, data.OutputMessageId, data.ReplyText);
            Trace("C02-External-06-01-chat-response", new { StatusCode = (int)response.StatusCode,
                Body = await response.Content.ReadAsStringAsync() });
            return response;
        });
        using var line = new CaptureHandler((_, _) => Task.FromResult(LineReplySamples.SuccessResponse()));
        using var host = new GatewayApplicationFactory(chat, line);
        host.Settings["Character:Id"] = data.CharacterId;
        host.Settings["Chat:RequestTimeoutSeconds"] = "5";
        var body = data.Body();
        Record("C02-External-06-01", host, data, body, LineWebhookSamples.Sign(body),
            new { HoldUntilReleased = true, NoLineMilliseconds = 250, ChatStatus = 200, LineStatus = 200, ExpectedChatCount = 1, ExpectedLineCount = 1 });
        using var client = host.CreateClient();
        using var request = LineWebhookSamples.Incoming(body, LineWebhookSamples.Sign(body));
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var sending = client.SendAsync(request, deadline.Token);
        try {
            var sent = await chat.NextAsync();
            ChatV02Assertions.AssertRequest(sent, data, "https://chat.test/chat/v0.2/generate-replies");
            await line.AssertNoPendingRequestsAsync();
            release.TrySetResult();
            using var response = await sending.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            LineReplyAssertions.AssertRequest(await line.NextAsync(), data, data.ReplyText);
            await Task.WhenAll(chat.AssertNoPendingRequestsAsync(), line.AssertNoPendingRequestsAsync());
            await host.StopHostAsync();
            Assert.Single(chat.Requests); Assert.Single(line.Requests);
            Trace("C02-External-06-01-observed", new { Chat = sent, Line = line.Requests });
        }
        finally {
            release.TrySetResult();
            deadline.Cancel();
            try { using var response = await sending.WaitAsync(TimeSpan.FromSeconds(10)); }
            catch (OperationCanceledException) { }
            finally { await host.StopHostAsync(); }
        }
    }
}
