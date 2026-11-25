#if PHASE2_GATEWAY_TESTS
// SPDX-License-Identifier: MIT
using System.Collections.Concurrent;
using LineBot.Api.Chat;
using LineBot.Api.Processing;
using LineBot.Api.Queueing;
using LineBot.Api.Workers;
using LineBot.Api.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace LineBot.Api.Tests.InternalContracts.Support;

internal sealed class Phase2QueueDouble : ILineWebhookEventQueue
{
    internal ConcurrentQueue<QueueItem> Writes { get; } = new();
    internal Func<QueueItem, QueueWriteResult> Write { get; init; } = _ => QueueWriteResult.Accepted;
    private readonly TaskCompletionSource<Func<QueueItem, CancellationToken, Task>> _receive =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private CancellationToken _stopping;
    private readonly object _sync = new();
    private readonly List<Task> _active = new();

    public Task<QueueWriteResult> TryEnqueueAsync(QueueItem item, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Writes.Enqueue(item);
        return Task.FromResult(Write(item));
    }

    public async Task RunAsync(Func<QueueItem, CancellationToken, Task> processItem, CancellationToken cancellationToken = default)
    {
        _stopping = cancellationToken;
        _receive.TrySetResult(processItem);
        try { await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Task[] active;
            lock (_sync) active = _active.ToArray();
            try { await Task.WhenAll(active); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        }
    }

    internal async Task DeliverAsync(QueueItem item, CancellationToken token)
    {
        var process = await _receive.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await TrackAsync(process, item, token);
    }

    internal async Task DeliverUntilStoppedAsync(QueueItem item, CancellationTokenSource processing)
    {
        var process = await _receive.Task.WaitAsync(TimeSpan.FromSeconds(10));
        using var registration = _stopping.Register(() => processing.Cancel());
        await TrackAsync(process, item, processing.Token);
    }
    private Task TrackAsync(Func<QueueItem, CancellationToken, Task> process, QueueItem item, CancellationToken token)
    {
        lock (_sync)
        {
            _stopping.ThrowIfCancellationRequested();
            var active = process(item, token);
            _active.Add(active);
            return active;
        }
    }
}

internal sealed class ResolverDouble(Func<QueueItem, CancellationToken, Task<IContractServices>> resolve) : IContractServicesResolver
{
    public Task<IContractServices> ResolveAsync(QueueItem item, CancellationToken cancellationToken = default) =>
        resolve(item, cancellationToken);
}

internal sealed class ContractServicesDouble(Func<QueueItem, CancellationToken, Task<ContractProcessingResult>> process) : IContractServices
{
    public Task<ContractProcessingResult> ProcessAsync(QueueItem item, CancellationToken cancellationToken = default) =>
        process(item, cancellationToken);
}

internal sealed class InputAssemblerDouble<TRequest>(
    Func<QueueItem, CancellationToken, Task<InputPreparationResult<TRequest>>> prepare) : IInputAssembler<TRequest>
    where TRequest : class, IChatServiceRequest
{
    internal ConcurrentQueue<(QueueItem Item, CancellationToken Token)> Calls { get; } = new();

    public Task<InputPreparationResult<TRequest>> PrepareAsync(QueueItem item, CancellationToken cancellationToken = default)
    {
        Calls.Enqueue((item, cancellationToken));
        return prepare(item, cancellationToken);
    }
}

internal sealed class ChatBoundaryDouble<TRequest>(
    Func<TRequest, CancellationToken, Task<ChatServiceResult>> generate, Action? onDispose = null) : IChatService<TRequest>, IDisposable
    where TRequest : class, IChatServiceRequest
{
    internal ConcurrentQueue<(TRequest Request, CancellationToken Token)> Calls { get; } = new();

    public Task<ChatServiceResult> GenerateReplyAsync(TRequest request, CancellationToken cancellationToken = default)
    {
        Calls.Enqueue((request, cancellationToken));
        return generate(request, cancellationToken);
    }

    public void Dispose() => onDispose?.Invoke();
}

internal sealed class ReplyBoundaryDouble : ILineReplyService
{
    internal ConcurrentQueue<(LineReplyContext Context, string[] Messages, CancellationToken Token)> Calls { get; } = new();
    internal Func<LineReplyContext, CancellationToken, Task<bool>> Respond { get; init; } =
        (_, _) => Task.FromResult(true);

    public Task<bool> SendReplyAsync(LineReplyContext context, List<string> messages, CancellationToken cancellationToken = default)
    {
        Calls.Enqueue((context, messages.ToArray(), cancellationToken));
        return Respond(context, cancellationToken);
    }
}

internal sealed class Phase2WorkerRig : IAsyncDisposable
{
    private readonly ServiceProvider _services;
    internal Phase2QueueDouble Queue { get; } = new();
    internal LogBook Logs { get; } = new();
    internal MessageWorkerService Worker { get; }

    internal Phase2WorkerRig(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILineWebhookEventQueue>(Queue);
        services.AddSingleton<Microsoft.Extensions.Logging.ILogger<MessageWorkerService>>(Logs.For<MessageWorkerService>());
        configure(services);
        _services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        Worker = ActivatorUtilities.CreateInstance<MessageWorkerService>(_services);
    }

    internal Task StartAsync() => Worker.StartAsync(CancellationToken.None);

    internal async Task StopAsync()
    {
        using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await Worker.StopAsync(limit.Token);
    }

    public async ValueTask DisposeAsync()
    {
        try { await StopAsync(); }
        finally
        {
            Worker.Dispose();
            await _services.DisposeAsync();
        }
    }
}
#endif
