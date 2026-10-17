// SPDX-License-Identifier: MIT
using LineBot.Api.Models;
using LineBot.Api.Services;
using LineBot.Api.Tests.Support;
using Microsoft.Extensions.Logging;
namespace LineBot.Api.Tests.InternalContracts;

public sealed class WorkerInteractionTests
{
    // Internal-05 / Internal-05-01: Verifies Worker passes text to an independent IChatService implementation.
    [Fact, Trait("Case", "Internal-05")]
    public async Task Worker_passes_text_to_an_independent_chat_implementation()
    {
        await using var rig = new WorkerRig();
        await rig.RunAsync(Samples.Job());
        var call = Assert.Single(rig.Chat.Calls);
        Assert.Equal("worker-test", call.Request.MessageText);
        // Conversation/author equality and configured limits belong to later repairs.
    }

    // Internal-06: Verifies Worker preserves the same job's reply arguments and cancellable tokens.
    [Theory, Trait("Case", "Internal-06")]
    [InlineData(false)] // Internal-06-01: first delivery
    [InlineData(true)]  // Internal-06-02: redelivery
    public async Task Worker_preserves_all_reply_arguments_for_the_same_job(bool redelivery)
    {
        await using var rig = new WorkerRig(); var job = Samples.Job(redelivery);
        var selected = new List<string> { Samples.RichText, "second-selected" };
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
        await using var rig = new WorkerRig();
        var selected = new List<string> { "chosen-by-chat", Samples.RichText };
        rig.Chat.Respond = (_, _) => Task.FromResult(new ChatServiceResult { Status = status, Messages = selected });
        await rig.RunAsync(Samples.Job());
        Assert.Equal(selected, Assert.Single(rig.Reply.Calls).Messages);
    }

    // Internal-08 / Internal-08-01: Verifies stopping Worker actually cancels the token received by Chat.
    [Fact, Trait("Case", "Internal-08")]
    public async Task Stopping_worker_cancels_the_token_received_by_chat()
    {
        await using var rig = new WorkerRig();
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        rig.Chat.Respond = async (_, token) => {
            using var registration = token.Register(() => cancelled.TrySetResult());
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            throw new InvalidOperationException("The cancellable wait must not finish normally.");
        };
        rig.Queue.TryEnqueue(Samples.Job());
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
        await using var rig = new WorkerRig();
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        rig.Reply.Respond = async token => {
            using var registration = token.Register(() => cancelled.TrySetResult());
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return true;
        };
        rig.Queue.TryEnqueue(Samples.Job());
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
        await using var rig = new WorkerRig();
        rig.Reply.Respond = _ => Task.FromResult(false);
        await rig.RunAsync(Samples.Job());
        Assert.Single(rig.Chat.Calls); Assert.Single(rig.Reply.Calls);
        Assert.Contains(rig.Logs.Entries, e => e.Level >= LogLevel.Warning &&
            e.Values.TryGetValue("WebhookEventId", out var id) && Equals(id, "event-worker"));
        Assert.DoesNotContain(rig.Logs.Entries, e => e.Level == LogLevel.Information &&
            e.Message.Contains("Successfully processed", StringComparison.Ordinal));
        // Do not assert idempotency marks, lifetime, ownership or cleanup policy.
    }
}
