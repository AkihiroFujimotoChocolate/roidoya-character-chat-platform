#if PHASE2_GATEWAY_TESTS
// SPDX-License-Identifier: MIT
using System.Collections.Concurrent;
using LineBot.Api.Queueing;
using LineBot.Api.Queueing.Local;
using LineBot.Api.Tests.InternalContracts.Support;
using LineBot.Api.Tests.Support;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace LineBot.Api.Tests.InternalContracts.Queueing.Local;

public sealed class LocalQueueTests(ITestOutputHelper output) : DiagnosticTest(output)
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(10);

    [Theory(DisplayName = "C02-Internal-14: Local Queue enforces both waiting limits")]
    [Trait("Case", "C02-Internal-14")]
    [InlineData("C02-Internal-14-01", "Reply limit is smaller", 1, 3, 2, 1, false)]
    [InlineData("C02-Internal-14-02", "Local limit is smaller", 3, 1, 2, 1, false)]
    [InlineData("C02-Internal-14-03", "Reply limit defaults to fifteen", null, 20, 16, 15, false)]
    [InlineData("C02-Internal-14-04", "Local limit defaults to fifteen", 20, null, 16, 15, false)]
    [InlineData("C02-Internal-14-05", "One of six concurrent producers is accepted", 1, 1, 6, 1, true)]
    public async Task Queue_WaitingLimits_AcceptOnlyCapacity(string caseId, string description, int? replyMax, int? localMax,
        int total, int expectedAccepted, bool concurrent)
    {
        var values = Enumerable.Range(0, total).Select(_ => Phase2FlowSamples.Random()).ToArray();
        var settings = Settings(replyMax, localMax);
        Trace(caseId, new { values, description, settings, total, expectedAccepted, concurrent, WaitSeconds = 10, QuietMilliseconds = 250 });
        using var provider = new ServiceCollection().AddSingleton<IConfiguration>(
            new ConfigurationBuilder().AddInMemoryCollection(settings).Build()).AddLogging().BuildServiceProvider();
        var queue = ActivatorUtilities.CreateInstance<LocalLineWebhookEventQueue>(provider);
        using var limit = new CancellationTokenSource(Limit);
        var results = new List<(string EventId, QueueWriteResult Result)>();
        if (concurrent)
        {
            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var pending = values.Select(async data =>
            {
                await start.Task.WaitAsync(limit.Token);
                return (data.EventId, await queue.TryEnqueueAsync(data.Job(), limit.Token));
            }).ToArray();
            start.TrySetResult();
            results.AddRange(await Task.WhenAll(pending).WaitAsync(Limit));
        }
        else
        {
            foreach (var data in values)
                results.Add((data.EventId, await queue.TryEnqueueAsync(data.Job(), limit.Token).WaitAsync(Limit)));
            Assert.Equal(QueueWriteResult.Accepted, results[0].Result);
            Assert.Equal(QueueWriteResult.Full, results[^1].Result);
        }
        var accepted = results.Where(item => item.Result == QueueWriteResult.Accepted).Select(item => item.EventId).ToArray();
        Assert.Equal(expectedAccepted, accepted.Length);
        Assert.Equal(total - expectedAccepted, results.Count(item => item.Result == QueueWriteResult.Full));
        Assert.Equal(total, results.Select(item => item.EventId).Distinct().Count());

        using var stopping = new CancellationTokenSource();
        var received = new ConcurrentQueue<string>();
        var drained = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var extra = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var count = 0;
        var receive = queue.RunAsync((item, _) =>
        {
            received.Enqueue(Phase2FlowSamples.EventId(item));
            var observed = Interlocked.Increment(ref count);
            if (observed == expectedAccepted) drained.TrySetResult();
            if (observed > expectedAccepted) extra.TrySetResult();
            return Task.CompletedTask;
        }, stopping.Token);
        try
        {
            await drained.Task.WaitAsync(Limit);
            try { await extra.Task.WaitAsync(TimeSpan.FromMilliseconds(250)); }
            catch (TimeoutException) { }
        }
        finally
        {
            stopping.Cancel();
            try { await receive.WaitAsync(Limit); } catch (OperationCanceledException) when (stopping.IsCancellationRequested) { }
        }
        Assert.Equal(accepted.OrderBy(id => id), received.OrderBy(id => id));
        Trace(caseId + "-observed", new { Writes = results.Select(write => new { write.EventId, write.Result }).ToArray(), Received = received });
    }

    [Fact(DisplayName = "C02-Internal-14-06: An item being processed does not occupy a waiting slot")]
    [Trait("Case", "C02-Internal-14")]
    public async Task Queue_ProcessingItem_DoesNotConsumeWaitingCapacity()
    {
        var first = Phase2FlowSamples.Random();
        var second = Phase2FlowSamples.Random();
        var settings = Settings(1, 3);
        Trace("C02-Internal-14-06", new { first, second, settings, WaitSeconds = 10 });
        using var provider = new ServiceCollection().AddSingleton<IConfiguration>(
            new ConfigurationBuilder().AddInMemoryCollection(settings).Build()).AddLogging().BuildServiceProvider();
        var queue = ActivatorUtilities.CreateInstance<LocalLineWebhookEventQueue>(provider);
        using var limit = new CancellationTokenSource(Limit);
        using var stopping = new CancellationTokenSource();
        var entered = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Assert.Equal(QueueWriteResult.Accepted, await queue.TryEnqueueAsync(first.Job(), limit.Token).WaitAsync(Limit));
        var receive = queue.RunAsync(async (item, token) =>
        {
            entered.TrySetResult(Phase2FlowSamples.EventId(item));
            await release.Task.WaitAsync(token);
        }, stopping.Token);
        try
        {
            Assert.Equal(first.EventId, await entered.Task.WaitAsync(Limit));
            Assert.Equal(QueueWriteResult.Accepted, await queue.TryEnqueueAsync(second.Job(), limit.Token).WaitAsync(Limit));
        }
        finally
        {
            release.TrySetResult();
            stopping.Cancel();
            try { await receive.WaitAsync(Limit); } catch (OperationCanceledException) when (stopping.IsCancellationRequested) { }
        }
    }

    private static Dictionary<string, string?> Settings(int? replyMax, int? localMax)
    {
        var settings = new Dictionary<string, string?>();
        if (replyMax is int reply) settings.Add("Reply:QueueMaxSize", reply.ToString());
        if (localMax is int local) settings.Add("Local:Queue:MaxSize", local.ToString());
        return settings;
    }
}
#endif
