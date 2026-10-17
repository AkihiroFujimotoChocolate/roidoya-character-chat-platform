// SPDX-License-Identifier: MIT
using LineBot.Api.Models;
using LineBot.Api.Services;
using LineBot.Api.Tests.Support;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;
namespace LineBot.Api.Tests.InternalContracts;

public sealed class WorkerInteractionTests : DiagnosticTest
{
    public WorkerInteractionTests(ITestOutputHelper output) : base(output) { }

    // Internal-05 / Internal-05-01: Verifies Worker passes text to an independent IChatService implementation.
    [Fact, Trait("Case", "Internal-05")]
    public async Task Worker_passes_text_to_an_independent_chat_implementation()
    {
        var data = FlowSamples.CreateRandom(richMessages: true);
        await using var rig = new WorkerRig();
        Trace("Internal-05-01", new { Values = data, WaitLimitSeconds = 10 }, rig.Settings);
        await rig.RunAsync(data.Job());
        var call = Assert.Single(rig.Chat.Calls);
        Assert.Equal(data.Text, call.Request.MessageText);
        // Conversation/author equality and configured limits belong to later repairs.
    }

    // Internal-06: Verifies Worker preserves the same job's reply arguments and cancellable tokens.
    [Theory, Trait("Case", "Internal-06")]
    [InlineData(false)] // Internal-06-01: first delivery
    [InlineData(true)]  // Internal-06-02: redelivery
    public async Task Worker_preserves_all_reply_arguments_for_the_same_job(bool redelivery)
    {
        var data = FlowSamples.CreateRandom(richMessages: true);
        await using var rig = new WorkerRig();
        Trace(redelivery ? "Internal-06-02" : "Internal-06-01", new { Values = data, redelivery, WaitLimitSeconds = 10 }, rig.Settings);
        var job = data.Job(redelivery);
        var selected = data.Messages.ToList();
        rig.Chat.Respond = (_, _) => Task.FromResult(new ChatServiceResult { Status = "ok", Messages = selected });
        await rig.RunAsync(job);
        var reply = Assert.Single(rig.Reply.Calls);
        Assert.Equal(job.ReplyToken, reply.Token);
        Assert.Equal(selected, reply.Messages);
        Assert.Equal(job.WebhookEventId, reply.EventId);
        Assert.Equal(redelivery, reply.Redelivery);
        Assert.True(reply.Cancellation.CanBeCanceled);
        Assert.True(Assert.Single(rig.Chat.Calls).Token.CanBeCanceled);
    }

    // Internal-07: Verifies Worker preserves messages already selected by Chat for each status.
    [Theory, Trait("Case", "Internal-07")]
    [InlineData("provider_error")]   // Internal-07-01: provider_error with messages already selected by Chat
    [InlineData("content_filtered")] // Internal-07-02: content_filtered with messages already selected by Chat
    public async Task Worker_does_not_replace_the_messages_selected_by_chat(string status)
    {
        var data = FlowSamples.CreateRandom(richMessages: true);
        await using var rig = new WorkerRig();
        Trace(status == "provider_error" ? "Internal-07-01" : "Internal-07-02", new { Values = data, status, WaitLimitSeconds = 10 }, rig.Settings);
        var selected = data.Messages.ToList();
        rig.Chat.Respond = (_, _) => Task.FromResult(new ChatServiceResult { Status = status, Messages = selected });
        await rig.RunAsync(data.Job());
        Assert.Equal(selected, Assert.Single(rig.Reply.Calls).Messages);
    }

    // Internal-08 / Internal-08-01: Verifies stopping Worker actually cancels the token received by Chat.
    [Fact, Trait("Case", "Internal-08")]
    public async Task Stopping_worker_cancels_the_token_received_by_chat()
    {
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
        await rig.Worker.StartAsync(CancellationToken.None);
        await rig.Chat.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await rig.StopAsync();
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(Assert.Single(rig.Chat.Calls).Token.IsCancellationRequested);
        Assert.Empty(rig.Reply.Calls);
    }

    // Internal-09 / Internal-09-01: Verifies stopping Worker actually cancels the token received by reply.
    [Fact, Trait("Case", "Internal-09")]
    public async Task Stopping_worker_cancels_the_token_received_by_reply()
    {
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
        await rig.Worker.StartAsync(CancellationToken.None);
        await rig.Reply.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await rig.StopAsync();
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(Assert.Single(rig.Reply.Calls).Cancellation.IsCancellationRequested);
        Assert.True(Assert.Single(rig.Chat.Calls).Token.IsCancellationRequested);
    }

    // Internal-12 / Internal-12-01: Verifies a generated reply is not reported as delivered when sending fails.
    [Fact, Trait("Case", "Internal-12")]
    public async Task Generated_messages_are_not_reported_as_delivered_when_reply_fails()
    {
        var data = FlowSamples.CreateRandom(richMessages: true);
        await using var rig = new WorkerRig();
        Trace("Internal-12-01", new { Values = data, ReplyResult = false, WaitLimitSeconds = 10 }, rig.Settings);
        rig.Reply.Respond = _ => Task.FromResult(false);
        await rig.RunAsync(data.Job());
        Assert.Single(rig.Chat.Calls); Assert.Single(rig.Reply.Calls);
        Assert.Contains(rig.Logs.Entries, e => e.Level >= LogLevel.Warning &&
            e.Values.TryGetValue("WebhookEventId", out var id) && Equals(id, data.EventId));
        Assert.DoesNotContain(rig.Logs.Entries, e => e.Level == LogLevel.Information &&
            e.Message.Contains("Successfully processed", StringComparison.Ordinal));
        // Do not assert idempotency marks, lifetime, ownership or cleanup policy.
    }
}
