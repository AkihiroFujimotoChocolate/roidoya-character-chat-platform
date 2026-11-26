#if PHASE2_GATEWAY_TESTS
// SPDX-License-Identifier: MIT
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using LineBot.Api.Chat;
using LineBot.Api.Processing;
using LineBot.Api.Queueing;
using LineBot.Api.Tests.InternalContracts.Support;
using LineBot.Api.Tests.InternalContracts.Support.V02;
using LineBot.Api.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;
using IIdempotencyService = LineBot.Api.Services.IIdempotencyService;
using Request = LineBot.Api.Contracts.V0_2.ChatServiceRequest;

namespace LineBot.Api.Tests.InternalContracts.Worker;

public sealed class Phase2IdempotencyTests(ITestOutputHelper output) : DiagnosticTest(output)
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(10);

    [Theory(DisplayName = "Common-Internal-17-02–05: Normal delivery skips lookup and registers after reply success")]
    [Trait("Case", "Common-Internal-17")]
    [InlineData("Common-Internal-17-02", "JSON false skips lookup", "{\"isRedelivery\":false}")]
    [InlineData("Common-Internal-17-03", "Missing deliveryContext skips lookup", null)]
    [InlineData("Common-Internal-17-04", "String true skips lookup", "{\"isRedelivery\":\"true\"}")]
    [InlineData("Common-Internal-17-05", "Missing isRedelivery skips lookup", "{}")]
    public async Task Worker_NormalDelivery_RegistersAfterReply(string caseId, string description, string? deliveryContextJson)
    {
        var data = Phase2FlowSamples.Random();
        var job = JobWithDeliveryContext(data, deliveryContextJson);
        TraceInput(caseId, data, job, new { description, deliveryContextJson, LookupResult = true, Preparation = "Ready", Reply = true,
            Message = data.ReplyText, ExpectedChecks = 0, ExpectedResolve = 1, ExpectedProcess = 1, ExpectedReplies = 1,
            ExpectedMarks = 1, ExpectedOrder = "reply-success, mark" });
        var context = await data.PrepareReplyContextAsync();
        Trace(caseId + "-reply-context", new { context, data.EventId, data.ReplyToken });
        var calls = new WorkerCalls();
        var record = calls.Record(_ => true);
        var reply = calls.Reply((_, _) => Task.FromResult(true));
        var result = Ready(context, data.ReplyText);
        await using var rig = calls.CreateRig(record, reply, (_, _) => Task.FromResult(result));
        using var deadline = new CancellationTokenSource(Limit);
        await rig.StartAsync().WaitAsync(Limit);
        await rig.Queue.DeliverAsync(job, deadline.Token).WaitAsync(Limit);
        TraceCalls(caseId, calls, record, reply);
        Assert.Equal(("mark", data.EventId), Assert.Single(record.Calls));
        Assert.Single(reply.Calls);
        Assert.Equal(1, calls.Order.Count(op => op == "resolve"));
        Assert.Equal(1, calls.Order.Count(op => op == "process"));
        Assert.Equal(new[] { "reply-success", "mark" }, calls.Order.Where(op => op is "reply-success" or "mark").ToArray());
    }

    [Fact(DisplayName = "Common-Internal-17-06: Unprocessed redelivery is looked up and registered after reply")]
    [Trait("Case", "Common-Internal-17")]
    public async Task Worker_UnprocessedRedelivery_ContinuesAndRegisters()
    {
        const string caseId = "Common-Internal-17-06";

        var data = Phase2FlowSamples.Random();
        var job = JobWithDeliveryContext(data, """{"isRedelivery":true}""");
        TraceInput(caseId, data, job, new { LookupResult = false, Preparation = "Ready", Reply = true, Message = data.ReplyText,
            ExpectedOrder = "check, resolve, process, reply-success, mark", CreatedAtLookup = 0, ScopeCount = 1 });
        var context = await data.PrepareReplyContextAsync();
        Trace(caseId + "-reply-context", new { context, data.EventId, data.ReplyToken });
        var calls = new WorkerCalls();
        var createdAtLookup = new ConcurrentQueue<int>();
        var record = calls.Record(_ =>
        {
            createdAtLookup.Enqueue(calls.Created.Count);
            return false;
        });
        var reply = calls.Reply((_, _) => Task.FromResult(true));
        var result = Ready(context, data.ReplyText);
        await using var rig = calls.CreateRig(record, reply, (_, _) => Task.FromResult(result));
        using var deadline = new CancellationTokenSource(Limit);
        await rig.StartAsync().WaitAsync(Limit);
        await rig.Queue.DeliverAsync(job, deadline.Token).WaitAsync(Limit);
        TraceCalls(caseId, calls, record, reply);
        Trace(caseId + "-lookup", new { Counts = createdAtLookup.ToArray() });
        Assert.Equal(new[] { 0 }, createdAtLookup.ToArray());
        Assert.Equal(Assert.Single(calls.Created), Assert.Single(calls.Disposed));
        Assert.Equal(new[] { ("check", data.EventId), ("mark", data.EventId) }, record.Calls.ToArray());
        Assert.Equal("check", calls.Order.First());
        Assert.Single(reply.Calls);
        Assert.Equal(1, calls.Order.Count(op => op == "resolve"));
        Assert.Equal(1, calls.Order.Count(op => op == "process"));
        Assert.Equal(new[] { "reply-success", "mark" }, calls.Order.Where(op => op is "reply-success" or "mark").ToArray());
    }

    [Fact(DisplayName = "Common-Internal-17-07: Processed redelivery skips the remaining boundaries")]
    [Trait("Case", "Common-Internal-17")]
    public async Task Worker_ProcessedRedelivery_CompletesAfterLookup()
    {
        const string caseId = "Common-Internal-17-07";

        var data = Phase2FlowSamples.Random();
        var job = JobWithDeliveryContext(data, """{"isRedelivery":true}""");
        TraceInput(caseId, data, job, new { LookupResult = true, Preparation = "Ready", Reply = true, Message = data.ReplyText,
            ExpectedOrder = "check only", ExpectedScope = 0, ExpectedChat = 0, ExpectedReplies = 0, ExpectedMarks = 0 });
        var context = await data.PrepareReplyContextAsync();
        Trace(caseId + "-reply-context", new { context, data.EventId, data.ReplyToken });
        var calls = new WorkerCalls();
        var record = calls.Record(_ => true);
        var reply = calls.Reply((_, _) => Task.FromResult(true));
        var chat = new ChatBoundaryDouble<Request>((_, _) => Task.FromResult(
            new ChatServiceResult { Status = "ok", Messages = [data.ReplyText] }));
        await using var rig = calls.CreateRig(record, reply, async (_, token) =>
        {
            var result = await chat.GenerateReplyAsync(data.ChatV02(), token);
            return new ContractProcessingResult(PreparationOutcome.Ready, result, context, null);
        });
        using var deadline = new CancellationTokenSource(Limit);
        await rig.StartAsync().WaitAsync(Limit);
        await rig.Queue.DeliverAsync(job, deadline.Token).WaitAsync(Limit);
        TraceCalls(caseId, calls, record, reply);
        Trace(caseId + "-chat", new { Count = chat.Calls.Count });
        Assert.Equal(("check", data.EventId), Assert.Single(record.Calls));
        Assert.Equal(new[] { "check" }, calls.Order.ToArray());
        Assert.Empty(calls.Created);
        Assert.Empty(chat.Calls);
        Assert.Empty(reply.Calls);
    }

    [Fact(DisplayName = "Common-Internal-17-08: A held reply is registered only after it succeeds")]
    [Trait("Case", "Common-Internal-17")]
    public async Task Worker_HeldReply_RegistersOnlyAfterSuccess()
    {
        const string caseId = "Common-Internal-17-08";

        var data = Phase2FlowSamples.Random();
        var job = data.Job();
        TraceInput(caseId, data, job, new { LookupResult = false, Preparation = "Ready", Message = data.ReplyText,
            Reply = "wait for release, then succeed", MarksBeforeRelease = 0, ExpectedOrder = "reply-success, mark" });
        var context = await data.PrepareReplyContextAsync();
        Trace(caseId + "-reply-context", new { context, data.EventId, data.ReplyToken });
        var calls = new WorkerCalls();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var record = calls.Record(_ => false);
        var reply = calls.Reply(async (_, token) =>
        {
            started.TrySetResult();
            await release.Task.WaitAsync(Limit, token);
            return true;
        });
        var result = Ready(context, data.ReplyText);
        await using var rig = calls.CreateRig(record, reply, (_, _) => Task.FromResult(result));
        using var cancellation = new CancellationTokenSource();
        await rig.StartAsync().WaitAsync(Limit);
        var processing = rig.Queue.DeliverAsync(job, cancellation.Token);
        try
        {
            await started.Task.WaitAsync(Limit);
            Assert.DoesNotContain(record.Calls, call => call.Operation == "mark");
            release.TrySetResult();
            await processing.WaitAsync(Limit);
            TraceCalls(caseId, calls, record, reply);
            Assert.Equal(("mark", data.EventId), Assert.Single(record.Calls));
            Assert.Equal(new[] { "reply-success", "mark" }, calls.Order.Where(op => op is "reply-success" or "mark").ToArray());
        }
        finally
        {
            release.TrySetResult();
            cancellation.Cancel();
        }
    }

    [Fact(DisplayName = "Common-Internal-17-09: A reply returning false leaves the record empty")]
    [Trait("Case", "Common-Internal-17")]
    public async Task Worker_FailedReply_DoesNotRegister()
    {
        const string caseId = "Common-Internal-17-09";

        var data = Phase2FlowSamples.Random();
        var job = data.Job();
        TraceInput(caseId, data, job, new { LookupResult = false, Preparation = "Ready", Message = data.ReplyText,
            Reply = false, ExpectedReplies = 1, ExpectedMarks = 0 });
        var context = await data.PrepareReplyContextAsync();
        Trace(caseId + "-reply-context", new { context, data.EventId, data.ReplyToken });
        var calls = new WorkerCalls();
        var record = calls.Record(_ => false);
        var reply = calls.Reply((_, _) => Task.FromResult(false));
        var result = Ready(context, data.ReplyText);
        await using var rig = calls.CreateRig(record, reply, (_, _) => Task.FromResult(result));
        using var deadline = new CancellationTokenSource(Limit);
        await rig.StartAsync().WaitAsync(Limit);
        await rig.Queue.DeliverAsync(job, deadline.Token).WaitAsync(Limit);
        TraceCalls(caseId, calls, record, reply);
        Assert.Single(reply.Calls);
        Assert.DoesNotContain(record.Calls, call => call.Operation == "mark");
    }

    [Theory(DisplayName = "Common-Internal-17-10–11: No reply is sent or registered for empty messages or failed preparation")]
    [Trait("Case", "Common-Internal-17")]
    [InlineData("Common-Internal-17-10", "Ready with empty messages skips reply and registration", PreparationOutcome.Ready, null)]
    [InlineData("Common-Internal-17-11", "Failed preparation skips reply and registration", PreparationOutcome.Failed, "assembly-failed-test")]
    public async Task Worker_WithoutReply_DoesNotSendOrRegister(string caseId, string description,
        PreparationOutcome outcome, string? error)
    {
        var data = Phase2FlowSamples.Random();
        var job = data.Job();
        TraceInput(caseId, data, job, new { description, LookupResult = false, Preparation = outcome.ToString(), error,
            ReadyMessages = Array.Empty<string>(), ReplyIfCalled = true, ExpectedProcess = 1, ExpectedReplies = 0, ExpectedMarks = 0 });
        var context = await data.PrepareReplyContextAsync();
        Trace(caseId + "-reply-context", new { context, data.EventId, data.ReplyToken });
        var calls = new WorkerCalls();
        var record = calls.Record(_ => false);
        var reply = calls.Reply((_, _) => Task.FromResult(true));
        var result = outcome == PreparationOutcome.Ready
            ? new ContractProcessingResult(outcome, new ChatServiceResult { Status = "ok", Messages = [] }, context, null)
            : new ContractProcessingResult(outcome, null, null, error);
        await using var rig = calls.CreateRig(record, reply, (_, _) => Task.FromResult(result));
        using var deadline = new CancellationTokenSource(Limit);
        await rig.StartAsync().WaitAsync(Limit);
        await rig.Queue.DeliverAsync(job, deadline.Token).WaitAsync(Limit);
        TraceCalls(caseId, calls, record, reply);
        Assert.Equal(1, calls.Order.Count(op => op == "process"));
        Assert.Empty(reply.Calls);
        Assert.DoesNotContain(record.Calls, call => call.Operation == "mark");
    }

    [Fact(DisplayName = "Common-Internal-17-13: Worker stop cancels the held reply and leaves the record empty")]
    [Trait("Case", "Common-Internal-17")]
    public async Task Worker_StopDuringReply_CancelsWithoutRegistering()
    {
        const string caseId = "Common-Internal-17-13";

        var data = Phase2FlowSamples.Random();
        var job = data.Job();
        TraceInput(caseId, data, job, new { LookupResult = false, Preparation = "Ready", Message = data.ReplyText,
            Sequence = "reply start, Worker stop, token cancellation, callback completion, stop completion", ExpectedMarks = 0 });
        var context = await data.PrepareReplyContextAsync();
        Trace(caseId + "-reply-context", new { context, data.EventId, data.ReplyToken });
        var calls = new WorkerCalls();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var record = calls.Record(_ => false);
        var reply = calls.Reply(async (_, token) =>
        {
            using var registration = token.Register(() => cancelled.TrySetResult());
            started.TrySetResult();
            await cancelled.Task.WaitAsync(Limit);
            token.ThrowIfCancellationRequested();
            return true;
        });
        var result = Ready(context, data.ReplyText);
        await using var rig = calls.CreateRig(record, reply, (_, _) => Task.FromResult(result));
        using var cancellation = new CancellationTokenSource();
        await rig.StartAsync().WaitAsync(Limit);
        var processing = rig.Queue.DeliverUntilStoppedAsync(job, cancellation);
        try
        {
            await started.Task.WaitAsync(Limit);
            var stopping = rig.StopAsync();
            await cancelled.Task.WaitAsync(Limit);
            try { await processing.WaitAsync(Limit); }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
            await stopping.WaitAsync(Limit);
            TraceCalls(caseId, calls, record, reply);
            var replyToken = Assert.Single(reply.Calls).Token;
            Assert.Equal(cancellation.Token, replyToken);
            Assert.True(replyToken.IsCancellationRequested);
            Assert.DoesNotContain(record.Calls, call => call.Operation == "mark");
        }
        finally { cancellation.Cancel(); }
    }

    [Fact(DisplayName = "Common-Internal-17-14: A throwing reply leaves the record empty and disposes the scope")]
    [Trait("Case", "Common-Internal-17")]
    public async Task Worker_ReplyException_DisposesScopeWithoutRegistering()
    {
        const string caseId = "Common-Internal-17-14";

        var data = Phase2FlowSamples.Random();
        var job = data.Job();
        TraceInput(caseId, data, job, new { LookupResult = false, Preparation = "Ready", Message = data.ReplyText,
            ReplyException = "InvalidOperationException: idempotency-test", ExpectedReplies = 1, ExpectedMarks = 0, ExpectedDisposed = 1 });
        var context = await data.PrepareReplyContextAsync();
        Trace(caseId + "-reply-context", new { context, data.EventId, data.ReplyToken });
        var calls = new WorkerCalls();
        var record = calls.Record(_ => false);
        var reply = calls.Reply((_, _) => throw new InvalidOperationException("idempotency-test"));
        var result = Ready(context, data.ReplyText);
        await using var rig = calls.CreateRig(record, reply, (_, _) => Task.FromResult(result));
        using var deadline = new CancellationTokenSource(Limit);
        await rig.StartAsync().WaitAsync(Limit);
        await rig.Queue.DeliverAsync(job, deadline.Token).WaitAsync(Limit);
        TraceCalls(caseId, calls, record, reply);
        Assert.Single(reply.Calls);
        Assert.DoesNotContain(record.Calls, call => call.Operation == "mark");
        Assert.Equal(Assert.Single(calls.Created), Assert.Single(calls.Disposed));
    }

    private static QueueItem JobWithDeliveryContext(Phase2FlowValues data, string? deliveryContextJson)
    {
        var root = JsonNode.Parse(data.Job().Webhook.GetRawText())!.AsObject();
        var item = root["event"]!.AsObject();
        if (deliveryContextJson is null)
            item.Remove("deliveryContext");
        else
            item["deliveryContext"] = JsonNode.Parse(deliveryContextJson);
        return (QueueItem)Phase2FlowSamples.Create(typeof(QueueItem),
            ("Webhook", JsonSerializer.SerializeToElement(root)), ("ConnectionId", data.ConnectionId));
    }

    private static ContractProcessingResult Ready(LineReplyContext context, string message) =>
        new(PreparationOutcome.Ready, new ChatServiceResult { Status = "ok", Messages = [message] }, context, null);

    private void TraceInput(string caseId, Phase2FlowValues data, QueueItem job, object conditions) =>
        Trace(caseId, new { Mode = "Worker direct", data, job.Webhook, job.ConnectionId,
            AccessToken = "access-token-test", conditions, WaitSeconds = 10 });

    private void TraceCalls(string caseId, WorkerCalls calls, IdempotencyDouble record, ReplyBoundaryDouble reply) =>
        Trace(caseId + "-observed", new
        {
            Order = calls.Order.ToArray(),
            Record = record.Calls.Select(c => new { c.Operation, c.EventId }).ToArray(),
            Replies = reply.Calls.Select(c => new { c.Context.WebhookEventId, c.Messages, c.Token.IsCancellationRequested }).ToArray(),
            Created = calls.Created.ToArray(), Disposed = calls.Disposed.ToArray()
        });

    private sealed class WorkerCalls
    {
        internal ConcurrentQueue<string> Order { get; } = new();
        internal ConcurrentQueue<Guid> Created { get; } = new();
        internal ConcurrentQueue<Guid> Disposed { get; } = new();

        internal IdempotencyDouble Record(Func<string, bool> check) => new()
        {
            Check = id => { Order.Enqueue("check"); return check(id); },
            Registered = _ => Order.Enqueue("mark")
        };

        internal ReplyBoundaryDouble Reply(Func<LineReplyContext, CancellationToken, Task<bool>> respond) => new()
        {
            Respond = async (context, token) =>
            {
                try
                {
                    var success = await respond(context, token);
                    Order.Enqueue(success ? "reply-success" : "reply-failed");
                    return success;
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    Order.Enqueue("reply-cancelled");
                    throw;
                }
                catch
                {
                    Order.Enqueue("reply-threw");
                    throw;
                }
            }
        };

        internal Phase2WorkerRig CreateRig(IdempotencyDouble record, ReplyBoundaryDouble reply,
            Func<QueueItem, CancellationToken, Task<ContractProcessingResult>> process) => new(services =>
        {
            services.AddSingleton<IIdempotencyService>(record);
            services.AddScoped(_ =>
            {
                var marker = new ScopeMarker(Disposed);
                Created.Enqueue(marker.Id);
                return marker;
            });
            services.AddScoped<IContractServicesResolver>(provider =>
            {
                provider.GetRequiredService<ScopeMarker>();
                return new ResolverDouble((_, _) =>
                {
                    Order.Enqueue("resolve");
                    return Task.FromResult<IContractServices>(new ContractServicesDouble((item, token) =>
                    {
                        Order.Enqueue("process");
                        return process(item, token);
                    }));
                });
            });
            services.AddScoped<ILineReplyService>(_ => reply);
        });
    }

    private sealed class ScopeMarker(ConcurrentQueue<Guid> disposed) : IDisposable
    {
        internal Guid Id { get; } = Guid.NewGuid();
        public void Dispose() => disposed.Enqueue(Id);
    }
}
#endif
