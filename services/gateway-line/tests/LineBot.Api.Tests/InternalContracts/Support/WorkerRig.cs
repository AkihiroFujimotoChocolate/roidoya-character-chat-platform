// SPDX-License-Identifier: MIT
using System.Collections.Concurrent;
using LineBot.Api.Models;
using LineBot.Api.Services;
using LineBot.Api.Tests.Support;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LineBot.Api.Tests.InternalContracts.Support;

// Bounded, per-test queue. Its second dequeue is a deterministic observation
// that the preceding job has finished; it does not test queue capacity or races.
internal sealed class ObservedQueue : ILocalQueueService
{
    private readonly ConcurrentQueue<QueueItem> _items = new();
    private int _accepted;
    private int _taken;
    internal TaskCompletionSource<QueueItem> Accepted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource AfterJob { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public bool TryEnqueue(QueueItem item) { _items.Enqueue(item); Interlocked.Increment(ref _accepted); Accepted.TrySetResult(item); return true; }
    public QueueItem? TryDequeue() {
        if (_items.TryDequeue(out var item)) { Interlocked.Increment(ref _taken); return item; }
        if (Volatile.Read(ref _taken) > 0) AfterJob.TrySetResult();
        return null;
    }
    public int Count => _items.Count;
    public bool IsFull => false;
    internal int AcceptedCount => Volatile.Read(ref _accepted);
}
internal sealed class TestLock : IUserLockService
{
    public bool TryAcquireLock(string key) => true;
    public void ReleaseLock(string key) { }
    public bool IsLocked(string key) => false;
}
internal sealed class TestIdempotency : IIdempotencyService
{
    public bool IsProcessed(string eventId) => false;
    public void MarkAsProcessed(string eventId) { }
}
internal sealed class ChatDouble : IChatService
{
    internal ConcurrentQueue<(ChatServiceRequest Request, CancellationToken Token)> Calls { get; } = new();
    internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal Func<ChatServiceRequest, CancellationToken, Task<ChatServiceResult>> Respond { get; set; } =
        (_, _) => Task.FromResult(new ChatServiceResult { Status = "ok", Messages = new() { "selected-a", "selected-b" } });
    public Task<ChatServiceResult> GenerateReplyAsync(ChatServiceRequest request, CancellationToken cancellationToken = default) {
        Calls.Enqueue((request, cancellationToken)); Started.TrySetResult(); return Respond(request, cancellationToken);
    }
}
internal sealed record ReplyCall(string Token, List<string> Messages, string EventId, bool Redelivery, CancellationToken Cancellation);
internal sealed class ReplyDouble : ILineReplyService
{
    internal ConcurrentQueue<ReplyCall> Calls { get; } = new();
    internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal Func<CancellationToken, Task<bool>> Respond { get; set; } = _ => Task.FromResult(true);
    public Task<bool> SendReplyAsync(string replyToken, List<string> messages, string webhookEventId, bool isRedelivery,
        CancellationToken cancellationToken = default) {
        Calls.Enqueue(new(replyToken, messages.ToList(), webhookEventId, isRedelivery, cancellationToken));
        Started.TrySetResult(); return Respond(cancellationToken);
    }
}
internal sealed class WorkerRig : IAsyncDisposable
{
    internal ObservedQueue Queue { get; } = new();
    internal ChatDouble Chat { get; } = new();
    internal ReplyDouble Reply { get; } = new();
    internal LogBook Logs { get; } = new();
    private readonly ServiceProvider _services;
    internal MessageWorkerService Worker { get; }
    internal IConfiguration Settings { get; } = InternalSamples.Config();
    internal WorkerRig() {
        var services = new ServiceCollection();
        services.AddSingleton<ILocalQueueService>(Queue);
        services.AddSingleton<IUserLockService>(new TestLock());
        services.AddSingleton<IIdempotencyService>(new TestIdempotency());
        services.AddSingleton<IChatService>(Chat);
        services.AddSingleton<ILineReplyService>(Reply);
        _services = services.BuildServiceProvider();
        Worker = new(_services, Logs.For<MessageWorkerService>(), Settings);
    }
    internal async Task RunAsync(QueueItem item) {
        Queue.TryEnqueue(item);
        await Worker.StartAsync(CancellationToken.None);
        await Queue.AfterJob.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }
    internal async Task StopAsync() {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await Worker.StopAsync(deadline.Token);
    }
    public async ValueTask DisposeAsync() {
        await StopAsync(); Worker.Dispose(); await _services.DisposeAsync();
    }
}
