// SPDX-License-Identifier: MIT
using LineBot.Api.Models;
using LineBot.Api.Services;
using LineBot.Api.Tests.Support;
using LineBot.Api.Tests.InternalContracts.Support;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;
namespace LineBot.Api.Tests.InternalContracts.Worker;

public sealed class WorkerInteractionTests : DiagnosticTest
{
    public WorkerInteractionTests(ITestOutputHelper output) : base(output) { }

    [Fact(DisplayName = "Internal-05: Worker passes input text to an independent Chat implementation")]
    [Trait("Case", "Internal-05")]
    public async Task Worker_ChatInput_PreservesText()
    {
        // Arrange
        var data = FlowSamples.CreateRandom(richMessages: true);
        await using var rig = new WorkerRig();
        Trace("Internal-05-01", new { Values = data, WaitLimitSeconds = 10 }, rig.Settings);
        // Act
        await rig.RunAsync(data.Job());
        // Assert
        var call = Assert.Single(rig.Chat.Calls);
        Assert.Equal(data.Text, call.Request.MessageText);
        // Conversation/author equality and configured limits belong to later repairs.
    }

    [Theory(DisplayName = "Internal-06: Worker preserves the same job reply arguments and cancellable tokens")]
    [Trait("Case", "Internal-06")]
    [InlineData("Internal-06-01", "Initial delivery preserves the same job reply arguments and cancellable tokens", false)]
    [InlineData("Internal-06-02", "Redelivery preserves the same job reply arguments and cancellable tokens", true)]
    public async Task Worker_ReplyCall_PreservesJobArguments(string caseId, string caseDescription, bool redelivery)
    {
        // Arrange
        var data = FlowSamples.CreateRandom(richMessages: true);
        await using var rig = new WorkerRig();
        Trace(caseId, new { Values = data, redelivery, WaitLimitSeconds = 10 }, rig.Settings);
        var job = data.Job(redelivery);
        var selected = data.Messages.ToList();
        rig.Chat.Respond = (_, _) => Task.FromResult(new ChatServiceResult { Status = "ok", Messages = selected });
        // Act
        await rig.RunAsync(job);
        // Assert
        var reply = Assert.Single(rig.Reply.Calls);
        Assert.Equal(job.ReplyToken, reply.Token);
        Assert.Equal(selected, reply.Messages);
        Assert.Equal(job.WebhookEventId, reply.EventId);
        Assert.Equal(redelivery, reply.Redelivery);
        Assert.True(reply.Cancellation.CanBeCanceled);
        Assert.True(Assert.Single(rig.Chat.Calls).Token.CanBeCanceled);
    }

    [Theory(DisplayName = "Internal-07: Worker preserves messages already selected by Chat")]
    [Trait("Case", "Internal-07")]
    [InlineData("Internal-07-01", "Messages selected with provider_error reach reply unchanged and in order", "provider_error")]
    [InlineData("Internal-07-02", "Messages selected with content_filtered reach reply unchanged and in order", "content_filtered")]
    public async Task Worker_SelectedMessages_PreservesContentAndOrder(string caseId, string caseDescription, string status)
    {
        // Arrange
        var data = FlowSamples.CreateRandom(richMessages: true);
        await using var rig = new WorkerRig();
        Trace(caseId, new { Values = data, status, WaitLimitSeconds = 10 }, rig.Settings);
        var selected = data.Messages.ToList();
        rig.Chat.Respond = (_, _) => Task.FromResult(new ChatServiceResult { Status = status, Messages = selected });
        // Act
        await rig.RunAsync(data.Job());
        // Assert
        Assert.Equal(selected, Assert.Single(rig.Reply.Calls).Messages);
    }

    [Fact(DisplayName = "Internal-08: Stopping Worker cancels the token received by Chat")]
    [Trait("Case", "Internal-08")]
    public async Task Worker_Stop_CancelsChatToken()
    {
        // Arrange
        var data = FlowSamples.CreateRandom(richMessages: true);
        await using var rig = new WorkerRig();
        Trace("Internal-08-01", new { Values = data, WaitLimitSeconds = 10 }, rig.Settings);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        rig.Chat.Respond = async (_, token) => {
            using var registration = token.Register(() => cancelled.TrySetResult());
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            throw new InvalidOperationException("The cancellable wait must not finish normally.");
        };
        rig.Queue.TryEnqueue(data.Job());
        // Act
        await rig.Worker.StartAsync(CancellationToken.None);
        await rig.Chat.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await rig.StopAsync();
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(10));
        // Assert
        Assert.True(Assert.Single(rig.Chat.Calls).Token.IsCancellationRequested);
        Assert.Empty(rig.Reply.Calls);
    }

    [Fact(DisplayName = "Internal-09: Stopping Worker cancels the token received by reply")]
    [Trait("Case", "Internal-09")]
    public async Task Worker_Stop_CancelsReplyToken()
    {
        // Arrange
        var data = FlowSamples.CreateRandom(richMessages: true);
        await using var rig = new WorkerRig();
        Trace("Internal-09-01", new { Values = data, WaitLimitSeconds = 10 }, rig.Settings);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        rig.Reply.Respond = async token => {
            using var registration = token.Register(() => cancelled.TrySetResult());
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return true;
        };
        rig.Queue.TryEnqueue(data.Job());
        // Act
        await rig.Worker.StartAsync(CancellationToken.None);
        await rig.Reply.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await rig.StopAsync();
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(10));
        // Assert
        Assert.True(Assert.Single(rig.Reply.Calls).Cancellation.IsCancellationRequested);
        Assert.True(Assert.Single(rig.Chat.Calls).Token.IsCancellationRequested);
    }

    [Fact(DisplayName = "Internal-12: Failed delivery is not reported as successful processing")]
    [Trait("Case", "Internal-12")]
    public async Task Worker_ReplyFailure_DoesNotReportDeliverySuccess()
    {
        // Arrange
        var data = FlowSamples.CreateRandom(richMessages: true);
        await using var rig = new WorkerRig();
        Trace("Internal-12-01", new { Values = data, ReplyResult = false, WaitLimitSeconds = 10 }, rig.Settings);
        rig.Reply.Respond = _ => Task.FromResult(false);
        // Act
        await rig.RunAsync(data.Job());
        // Assert
        Assert.Single(rig.Chat.Calls); Assert.Single(rig.Reply.Calls);
        Assert.Contains(rig.Logs.Entries, e => e.Level >= LogLevel.Warning &&
            e.Values.TryGetValue("WebhookEventId", out var id) && Equals(id, data.EventId));
        Assert.DoesNotContain(rig.Logs.Entries, e => e.Level == LogLevel.Information &&
            e.Message.Contains("Successfully processed", StringComparison.Ordinal));
        // Do not assert idempotency marks, lifetime, ownership or cleanup policy.
    }
}

