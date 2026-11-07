// SPDX-License-Identifier: MIT
using LineBot.Api.Models;
using LineBot.Api.Services;
using LineBot.Api.Tests.Support;
using LineBot.Api.Tests.InternalContracts.Support;
using LineBot.Api.Tests.ExternalContracts.V02.Support;
using LineBot.Api.Tests.ExternalContracts.V02.Chat;
using LineBot.Api.Tests.ExternalContracts.V01.Line;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit.Abstractions;
using GatewayApplicationFactory = LineBot.Api.Tests.ExternalContracts.V02.Support.GatewayApplicationFactory;
namespace LineBot.Api.Tests.InternalContracts.Composition.V02;

public sealed class HostedCancellationTests : DiagnosticTest
{
    public HostedCancellationTests(ITestOutputHelper output) : base(output) { }
    [Theory(DisplayName = "C02-Internal-12: Stopping the production host cancels its active operation")]
    [Trait("Case", "C02-Internal-12")]
    [InlineData("C02-Internal-12-01", "Host stop cancels active Chat before reply", true)]
    [InlineData("C02-Internal-12-02", "Host stop cancels active reply", false)]
    public async Task Host_Stop_CancelsActiveOperation(string caseId, string caseDescription, bool holdChat)
    {
        var data = ExternalSamples.Random();
        var chat = new ChatDouble();
        var reply = new ReplyDouble();
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var hold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (holdChat) {
            chat.Respond = async (_, token) => {
                using var registration = token.Register(() => { cancelled.TrySetResult(); hold.TrySetCanceled(token); });
                ready.TrySetResult();
                await hold.Task;
                throw new InvalidOperationException("The cancellable wait must not finish normally.");
            };
        }
        else {
            reply.Respond = async token => {
                using var registration = token.Register(() => { cancelled.TrySetResult(); hold.TrySetCanceled(token); });
                ready.TrySetResult();
                await hold.Task;
                return true;
            };
        }
        using var chatHttp = new CaptureHandler((sent, _) => Task.FromResult(ChatV02Samples.Ok(sent, data.OutputMessageId, data.ReplyText)));
        using var lineHttp = new CaptureHandler((_, _) => Task.FromResult(LineReplySamples.SuccessResponse()));
        await using var host = new GatewayApplicationFactory(chatHttp, lineHttp,
            configureTestServices: services => {
                services.RemoveAll<IChatService>();
                services.AddSingleton<IChatService>(chat);
                services.RemoveAll<ILineReplyService>();
                services.AddSingleton<ILineReplyService>(reply);
            });
        host.Settings["Character:Id"] = data.CharacterId;
        var body = data.Body();
        var signature = LineWebhookSamples.Sign(body);
        Trace(caseId, new { Values = data, BodyBase64 = Convert.ToBase64String(body), Signature = signature,
            FactorySettings = host.Settings, HoldChat = holdChat, ChatStatus = "ok",
            ChatMessages = new[] { "selected-a", "selected-b" }, ReplyResult = true,
            ExpectedChatCount = 1, ExpectedReplyCount = holdChat ? 0 : 1,
            ExpectedCancellation = true, WaitLimitSeconds = 10 });
        using var client = host.CreateClient();
        using var incoming = LineWebhookSamples.Incoming(body, signature);
        using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var sending = client.SendAsync(incoming, limit.Token);
        try {
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await host.StopHostAsync();
            await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var chatCall = Assert.Single(chat.Calls);
            if (holdChat) {
                Assert.True(chatCall.Token.IsCancellationRequested);
                Assert.Empty(reply.Calls);
            }
            else Assert.True(Assert.Single(reply.Calls).Cancellation.IsCancellationRequested);
            Trace(caseId + "-observed", new { Chat = chat.Calls.Select(c => new { c.Request, CanBeCanceled = c.Token.CanBeCanceled, IsCancellationRequested = c.Token.IsCancellationRequested }).ToArray(), Reply = reply.Calls.Select(c => new { c.Token, c.Messages, c.EventId, c.Redelivery, CanBeCanceled = c.Cancellation.CanBeCanceled, IsCancellationRequested = c.Cancellation.IsCancellationRequested }).ToArray(),
                Ready = ready.Task.IsCompletedSuccessfully, Cancelled = cancelled.Task.IsCompletedSuccessfully });
            using var response = await sending.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally {
            hold.TrySetCanceled();
            limit.Cancel();
            try { using var response = await sending.WaitAsync(TimeSpan.FromSeconds(10)); }
            catch (OperationCanceledException) { }
            finally { await host.StopHostAsync(); }
        }
    }
}
