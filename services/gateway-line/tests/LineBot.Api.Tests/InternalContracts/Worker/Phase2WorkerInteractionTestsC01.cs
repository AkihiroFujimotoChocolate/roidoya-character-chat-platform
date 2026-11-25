#if PHASE2_GATEWAY_TESTS
// SPDX-License-Identifier: MIT
using System.Collections.Concurrent;
using LineBot.Api.Chat;
using LineBot.Api.Processing;
using LineBot.Api.Queueing;
using LineBot.Api.Tests.InternalContracts.Support.V01;
using LineBot.Api.Tests.InternalContracts.Support;
using LineBot.Api.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;
using Request = LineBot.Api.Contracts.V0_1.ChatServiceRequest;

namespace LineBot.Api.Tests.InternalContracts.Worker;

public sealed class Phase2WorkerInteractionTestsC01(ITestOutputHelper output) : DiagnosticTest(output)
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(10);

    [Theory(DisplayName = "C01-Internal-05: Worker sends only nonempty replies with original context and token")]
    [Trait("Case", "C01-Internal-05")]
    [InlineData("C01-Internal-05-01", "Empty result does not call LINE", 0)]
    [InlineData("C01-Internal-05-02", "Two messages and their order reach the reply boundary", 2)]
    public async Task Worker_ReplyCall_PreservesArguments(string caseId, string description, int messageCount)
    {
        var data = Phase2FlowSamples.Random("0.1");
        var messages = data.Messages.Take(messageCount).ToArray();
        var context = await data.PrepareReplyContextAsync();
        var result = new ContractProcessingResult(PreparationOutcome.Ready, new() { Status = "ok", Messages = messages.ToList() }, context, null);
        var reply = new ReplyBoundaryDouble();
        await using var rig = new Phase2WorkerRig(services =>
        {
            services.AddScoped<IContractServicesResolver>(_ => new ResolverDouble((_, _) =>
                Task.FromResult<IContractServices>(new ContractServicesDouble((_, _) => Task.FromResult(result)))));
            services.AddScoped<ILineReplyService>(_ => reply);
        });
        using var token = new CancellationTokenSource(Limit);
        Trace(caseId, new { data, description, messages, context, WaitSeconds = 10 });
        await rig.StartAsync().WaitAsync(Limit);
        await rig.Queue.DeliverAsync(data.Job(), token.Token).WaitAsync(Limit);
        if (messageCount == 0)
        {
            Assert.Empty(reply.Calls);
            return;
        }
        var call = Assert.Single(reply.Calls);
        Assert.Equal(context, call.Context);
        Assert.IsType<LineReplyToken>(call.Context.ReplyToken);
        Assert.IsType<ChannelAccessToken>(call.Context.ChannelAccessToken);
        Assert.Equal(data.EventId, call.Context.WebhookEventId);
        Assert.Equal(messages, call.Messages);
        Assert.True(call.Token.CanBeCanceled);
        Assert.Equal(token.Token, call.Token);
    }

    [Theory(DisplayName = "C01-Internal-08: Worker stop propagates cancellation and processing completes")]
    [Trait("Case", "C01-Internal-08")]
    [InlineData("C01-Internal-08-01", "Stop with Chat waiting before any reply", true)]
    [InlineData("C01-Internal-08-02", "Stop with LINE reply waiting after Chat", false)]
    public async Task Worker_Stop_CancelsActiveBoundary(string caseId, string description, bool cancelChat)
    {
        var data = Phase2FlowSamples.Random("0.1");
        var context = await data.PrepareReplyContextAsync();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task WaitForCancellation(CancellationToken token)
        {
            using var registration = token.Register(() => cancelled.TrySetResult());
            started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
        }
        var assembler = new InputAssemblerDouble<Request>((_, _) =>
            Task.FromResult(new InputPreparationResult<Request>(PreparationOutcome.Ready, data.ChatV01(), context, null)));
        var chat = new ChatBoundaryDouble<Request>(async (_, token) =>
        {
            if (cancelChat) await WaitForCancellation(token);
            return new ChatServiceResult { Status = "ok", Messages = [data.ReplyText] };
        });
        var reply = new ReplyBoundaryDouble { Respond = async (_, token) =>
        {
            await WaitForCancellation(token);
            return true;
        } };
        using var itemCancellation = new CancellationTokenSource();
        await using var rig = new Phase2WorkerRig(services =>
        {
            services.AddScoped<IContractServicesResolver>(_ => new ResolverDouble((_, _) =>
                Task.FromResult<IContractServices>(new ContractServices<Request>(assembler, chat))));
            services.AddScoped<ILineReplyService>(_ => reply);
        });
        Trace(caseId, new { data, description, cancelChat, WaitSeconds = 10, ExpectedChat = 1, ExpectedReply = cancelChat ? 0 : 1 });
        await rig.StartAsync().WaitAsync(Limit);
        var processing = rig.Queue.DeliverUntilStoppedAsync(data.Job(), itemCancellation);
        await started.Task.WaitAsync(Limit);
        var stopping = rig.StopAsync();
        await cancelled.Task.WaitAsync(Limit);
        try { await processing.WaitAsync(Limit); } catch (OperationCanceledException) { }
        await stopping.WaitAsync(Limit);
        var chatToken = Assert.Single(chat.Calls).Token;
        Assert.Equal(itemCancellation.Token, chatToken);
        Assert.True(chatToken.IsCancellationRequested);
        if (cancelChat) Assert.Empty(reply.Calls);
        else
        {
            var replyToken = Assert.Single(reply.Calls).Token;
            Assert.Equal(itemCancellation.Token, replyToken);
            Assert.True(replyToken.IsCancellationRequested);
        }
    }

    [Theory(DisplayName = "C01-Internal-10: Worker preserves selected messages for unsuccessful Chat statuses")]
    [Trait("Case", "C01-Internal-10")]
    [InlineData("C01-Internal-10-01", "Preserve ordered messages from provider_error", "provider_error")]
    [InlineData("C01-Internal-10-02", "Preserve ordered messages from content_filtered", "content_filtered")]
    public async Task Worker_SelectedMessages_PreservesContentAndOrder(string caseId, string description, string status)
    {
        var data = Phase2FlowSamples.Random("0.1");
        var context = await data.PrepareReplyContextAsync();
        var chatResult = new ChatServiceResult { Messages = data.Messages.ToList(), Status = status };
        var reply = new ReplyBoundaryDouble();
        await using var rig = new Phase2WorkerRig(services =>
        {
            services.AddScoped<IContractServicesResolver>(_ => new ResolverDouble((_, _) =>
                Task.FromResult<IContractServices>(new ContractServicesDouble((_, _) =>
                    Task.FromResult(new ContractProcessingResult(PreparationOutcome.Ready, chatResult, context, null))))));
            services.AddScoped<ILineReplyService>(_ => reply);
        });
        Trace(caseId, new { data, description, status, ExpectedReplies = 1, WaitSeconds = 10 });
        await rig.StartAsync().WaitAsync(Limit);
        await rig.Queue.DeliverAsync(data.Job(), CancellationToken.None).WaitAsync(Limit);
        Assert.Equal(data.Messages, Assert.Single(reply.Calls).Messages);
    }
}
#endif
