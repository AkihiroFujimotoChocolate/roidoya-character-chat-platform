#if PHASE2_GATEWAY_TESTS
// SPDX-License-Identifier: MIT
using System.Collections.Concurrent;
using LineBot.Api.Chat;
using LineBot.Api.Processing;
using LineBot.Api.Queueing;
using LineBot.Api.Tests.InternalContracts.Support.V02;
using LineBot.Api.Tests.InternalContracts.Support;
using LineBot.Api.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;
using Request = LineBot.Api.Contracts.V0_2.ChatServiceRequest;

namespace LineBot.Api.Tests.InternalContracts.Worker;

public sealed class WorkerScopeTests(ITestOutputHelper output) : DiagnosticTest(output)
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(10);

    [Fact(DisplayName = "C02-Internal-16-01: Concurrent items have distinct disposable scopes")]
    [Trait("Case", "C02-Internal-16")]
    public async Task Worker_ConcurrentItems_UseSeparateScopes()
    {
        var first = Phase2FlowSamples.Random();
        var second = Phase2FlowSamples.Random();
        var firstContext = await first.PrepareReplyContextAsync();
        var secondContext = await second.PrepareReplyContextAsync();
        var seen = new ConcurrentQueue<(string Event, string Boundary, Guid Scope)>();
        var disposed = new ConcurrentQueue<Guid>();
        var bothResolved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resolved = 0;
        await using var rig = new Phase2WorkerRig(services =>
        {
            services.AddScoped(_ => new ScopeMarker(disposed));
            services.AddScoped<IContractServicesResolver>(provider =>
            {
                var scope = provider.GetRequiredService<ScopeMarker>();
                return new ResolverDouble(async (job, token) =>
                {
                    var data = Phase2FlowSamples.EventId(job) == first.EventId ? first : second;
                    seen.Enqueue((data.EventId, "resolve", scope.Id));
                    if (Interlocked.Increment(ref resolved) == 2) bothResolved.TrySetResult();
                    await release.Task.WaitAsync(token);
                    return new ContractServicesDouble((item, _) =>
                    {
                        seen.Enqueue((Phase2FlowSamples.EventId(item), "process", scope.Id));
                        return Task.FromResult(new ContractProcessingResult(PreparationOutcome.Ready,
                            new ChatServiceResult { Status = "ok", Messages = [data.ReplyText] }, data == first ? firstContext : secondContext, null));
                    });
                });
            });
            services.AddScoped<ILineReplyService>(provider =>
            {
                var scope = provider.GetRequiredService<ScopeMarker>();
                return new ReplyBoundaryDouble { Respond = (context, _) =>
                {
                    seen.Enqueue((context.WebhookEventId, "reply", scope.Id));
                    return Task.FromResult(true);
                } };
            });
        });
        Trace("C02-Internal-16-01", new { first, second, Parallel = true, ExpectedScopes = 2, WaitSeconds = 10 });
        using var deadline = new CancellationTokenSource(Limit);
        await rig.StartAsync().WaitAsync(Limit);
        var processing = Task.WhenAll(rig.Queue.DeliverAsync(first.Job(), deadline.Token),
            rig.Queue.DeliverAsync(second.Job(), deadline.Token));
        try { await bothResolved.Task.WaitAsync(Limit); }
        finally { release.TrySetResult(); await processing.WaitAsync(Limit); }
        Trace("C02-Internal-16-01-observed", new
        {
            Boundaries = seen.Select(call => new { call.Event, call.Boundary, call.Scope }).ToArray(),
            Disposed = disposed.ToArray()
        });
        var scopeIds = new List<Guid>();
        foreach (var data in new[] { first, second })
        {
            var boundaries = seen.Where(call => call.Event == data.EventId).ToArray();
            Assert.Equal(new[] { "resolve", "process", "reply" }, boundaries.Select(call => call.Boundary));
            scopeIds.Add(Assert.Single(boundaries.Select(call => call.Scope).Distinct()));
        }
        Assert.NotEqual(scopeIds[0], scopeIds[1]);
        Assert.Equal(scopeIds.OrderBy(id => id), disposed.OrderBy(id => id));
    }

    [Fact(DisplayName = "C02-Internal-16-02: Resolver failure is logged and the next event still replies")]
    [Trait("Case", "C02-Internal-16")]
    public async Task Worker_ResolverThrows_DisposesScopeAndContinues()
    {
        var first = Phase2FlowSamples.Random();
        var second = Phase2FlowSamples.Random();
        var secondContext = await second.PrepareReplyContextAsync();
        var failure = new InvalidOperationException("resolver-failed-test");
        var reply = new ReplyBoundaryDouble();
        var disposed = new ConcurrentQueue<Guid>();
        var firstScope = Guid.Empty;
        var secondScope = Guid.Empty;
        var processed = new List<string>();
        await using var rig = new Phase2WorkerRig(services =>
        {
            services.AddScoped(_ => new ScopeMarker(disposed));
            services.AddScoped<IContractServicesResolver>(provider =>
            {
                var scope = provider.GetRequiredService<ScopeMarker>();
                return new ResolverDouble((job, _) =>
                {
                    if (Phase2FlowSamples.EventId(job) == first.EventId)
                    {
                        firstScope = scope.Id;
                        return Task.FromException<IContractServices>(failure);
                    }
                    secondScope = scope.Id;
                    return Task.FromResult<IContractServices>(new ContractServicesDouble((item, _) =>
                    {
                        processed.Add(Phase2FlowSamples.EventId(item));
                        return Task.FromResult(new ContractProcessingResult(PreparationOutcome.Ready,
                            new() { Status = "ok", Messages = [second.ReplyText] }, secondContext, null));
                    }));
                });
            });
            services.AddScoped<ILineReplyService>(_ => reply);
        });
        Trace("C02-Internal-16-02", new { first, second, Error = failure.Message, WaitSeconds = 10 });
        await rig.StartAsync().WaitAsync(Limit);
        var firstLog = rig.Logs.Entries.Count;
        await rig.Queue.DeliverAsync(first.Job(), CancellationToken.None).WaitAsync(Limit);
        var logs = rig.Logs.Entries.Skip(firstLog).ToArray();
        Trace("C02-Internal-16-02-observed", new { first.EventId, Logs = logs.Select(entry => new { entry.Category, entry.Level, entry.Message, entry.Values, Exception = entry.Exception?.ToString() }) });
        Assert.Contains(logs, entry => ReferenceEquals(failure, entry.Exception)
            || entry.Exception?.Message.Contains(failure.Message, StringComparison.Ordinal) == true
            || entry.Message.Contains(failure.Message, StringComparison.Ordinal)
            || entry.Values.Values.Any(value => value?.ToString()?.Contains(failure.Message, StringComparison.Ordinal) == true));
        Assert.Empty(reply.Calls);
        Assert.Equal(firstScope, Assert.Single(disposed));
        await rig.Queue.DeliverAsync(second.Job(), CancellationToken.None).WaitAsync(Limit);
        Assert.Equal(second.EventId, Assert.Single(processed));
        Assert.Equal(second.EventId, Assert.Single(reply.Calls).Context.WebhookEventId);
        Assert.NotEqual(firstScope, secondScope);
        Assert.Equal(new[] { firstScope, secondScope }, disposed.ToArray());
    }

    [Fact(DisplayName = "C02-Internal-16-03: Cancelling one queue token leaves another event independent")]
    [Trait("Case", "C02-Internal-16")]
    public async Task Worker_ItemCancellation_ReachesInputAssembly()
    {
        var first = Phase2FlowSamples.Random();
        var second = Phase2FlowSamples.Random();
        var firstContext = await first.PrepareReplyContextAsync();
        var secondContext = await second.PrepareReplyContextAsync();
        var resolverTokens = new ConcurrentDictionary<string, CancellationToken>();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var assembler = new InputAssemblerDouble<Request>(async (job, token) =>
        {
            var data = Phase2FlowSamples.EventId(job) == first.EventId ? first : second;
            if (data == first)
            {
                using var subscription = token.Register(() => cancelled.TrySetResult());
                started.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            }
            return new(PreparationOutcome.Ready, data.ChatV02(), data == first ? firstContext : secondContext, null);
        });
        var chat = new ChatBoundaryDouble<Request>((_, _) =>
            Task.FromResult(new ChatServiceResult { Status = "ok", Messages = [second.ReplyText] }));
        var reply = new ReplyBoundaryDouble();
        await using var rig = new Phase2WorkerRig(services =>
        {
            services.AddScoped<IContractServicesResolver>(_ => new ResolverDouble((job, token) =>
            {
                Assert.True(resolverTokens.TryAdd(Phase2FlowSamples.EventId(job), token));
                return Task.FromResult<IContractServices>(new ContractServices<Request>(assembler, chat));
            }));
            services.AddScoped<ILineReplyService>(_ => reply);
        });
        Trace("C02-Internal-16-03", new { first, second, ExpectedCancellation = first.EventId, WaitSeconds = 10 });
        using var token = new CancellationTokenSource(Limit);
        await rig.StartAsync().WaitAsync(Limit);
        var processing = rig.Queue.DeliverAsync(first.Job(), token.Token);
        try
        {
            await started.Task.WaitAsync(Limit);
            token.Cancel();
            await cancelled.Task.WaitAsync(Limit);
            try { await processing.WaitAsync(Limit); } catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            Assert.Equal(token.Token, resolverTokens[first.EventId]);
            Assert.Equal(token.Token, Assert.Single(assembler.Calls).Token);
            Assert.Empty(chat.Calls);
            Assert.Empty(reply.Calls);
            await rig.Queue.DeliverAsync(second.Job(), CancellationToken.None).WaitAsync(Limit);
            Assert.Equal(second.Text, Assert.Single(chat.Calls).Request.MessageText);
            Assert.Equal(second.EventId, Assert.Single(reply.Calls).Context.WebhookEventId);
        }
        finally { token.Cancel(); }
    }

    private sealed class ScopeMarker(ConcurrentQueue<Guid> disposed) : IDisposable
    {
        internal Guid Id { get; } = Guid.NewGuid();
        public void Dispose() => disposed.Enqueue(Id);
    }
}
#endif
