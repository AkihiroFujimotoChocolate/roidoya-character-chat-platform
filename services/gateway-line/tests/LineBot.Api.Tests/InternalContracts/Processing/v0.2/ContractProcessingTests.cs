#if PHASE2_GATEWAY_TESTS
// SPDX-License-Identifier: MIT
using LineBot.Api.Chat;
using LineBot.Api.Processing;
using LineBot.Api.Queueing;
using LineBot.Api.Tests.InternalContracts.Support.V02;
using LineBot.Api.Tests.ExternalContracts.V01.Line;
using LineBot.Api.Tests.ExternalContracts.V02.ConnectionManagement;
using LineBot.Api.Tests.ExternalContracts.V02.Support;
using LineBot.Api.Tests.InternalContracts.Support;
using LineBot.Api.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;
using Request = LineBot.Api.Contracts.V0_2.ChatServiceRequest;

namespace LineBot.Api.Tests.InternalContracts.Processing.V02;

public sealed class ContractProcessingTests(ITestOutputHelper output) : DiagnosticTest(output)
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(10);

    [Fact(DisplayName = "C02-Internal-04-01: Worker forwards the same queue item through both boundaries")]
    [Trait("Case", "C02-Internal-04")]
    public async Task Worker_ResolverAndProcessing_ReceiveSameQueueItem()
    {
        var data = Phase2FlowSamples.Random();
        var context = await data.PrepareReplyContextAsync();
        var item = data.Job();
        var received = new List<(string Operation, QueueItem Item)>();
        var processing = new ContractServicesDouble((job, _) =>
        {
            received.Add(("process", job));
            return Task.FromResult(new ContractProcessingResult(PreparationOutcome.Ready, new(), context, null));
        });
        var reply = new ReplyBoundaryDouble();
        await using var rig = new Phase2WorkerRig(services =>
        {
            services.AddScoped<IContractServicesResolver>(_ => new ResolverDouble((job, _) =>
            {
                received.Add(("resolve", job));
                return Task.FromResult<IContractServices>(processing);
            }));
            services.AddScoped<ILineReplyService>(_ => reply);
        });
        Trace("C02-Internal-04-01", new { data, ExpectedOrder = new[] { "resolve", "process" }, WaitSeconds = 10 });
        await rig.StartAsync().WaitAsync(Limit);
        await rig.Queue.DeliverAsync(item, CancellationToken.None).WaitAsync(Limit);
        Assert.Equal(new[] { "resolve", "process" }, received.Select(call => call.Operation));
        Assert.All(received, call =>
        {
            Assert.Equal(item, call.Item);
            Assert.Equal(data.ConnectionId, call.Item.ConnectionId);
            Assert.Equal(data.Text, call.Item.Webhook.GetProperty("event").GetProperty("message").GetProperty("text").GetString());
        });
    }

    [Fact(DisplayName = "C02-Internal-04-02: Input assembly preserves webhook values and maps connection data")]
    [Trait("Case", "C02-Internal-04")]
    public async Task InputAssembler_QueueItem_ProducesChatRequestAndReplyContext()
    {
        var data = Phase2FlowSamples.Random();
        var external = ExternalSamples.Random() with { ActorId = data.ActorId, PersonId = data.PersonId,
            ConnectionId = data.ConnectionId!, Destination = data.Destination, CharacterId = data.CharacterId };
        var registeredDestination = GeneratedValues.Id("line-destination");
        using var connections = ConnectionSamples.Capture(external, destination: registeredDestination);
        using var outboundChat = new CaptureHandler((_, _) => throw new InvalidOperationException("Unexpected Chat HTTP request."));
        using var outboundLine = new CaptureHandler((_, _) => throw new InvalidOperationException("Unexpected LINE HTTP request."));
        using var host = new GatewayApplicationFactory(outboundChat, outboundLine, connections);
        var chat = new ChatBoundaryDouble<Request>((_, _) =>
            Task.FromResult(new ChatServiceResult { Status = "ok", Messages = [data.ReplyText] }));
        Trace("C02-Internal-04-02", new { data, host.Settings, Connection = ConnectionSamples.Connection(external, registeredDestination),
            ExpectedChannelType = "line", ExpectedInstanceId = data.Destination, ExpectedChatCount = 1, WaitSeconds = 10 });
        using var scope = host.Services.CreateScope();
        var assembler = scope.ServiceProvider.GetRequiredKeyedService<IInputAssembler<Request>>("0.2");
        using var deadline = new CancellationTokenSource(Limit);
        try
        {
            var result = await new ContractServices<Request>(assembler, chat).ProcessAsync(data.Job(), deadline.Token).WaitAsync(Limit);
            Assert.Equal(PreparationOutcome.Ready, result.Outcome);
            var call = Assert.Single(chat.Calls);
            Assert.IsType<Request>(call.Request);
            Assert.True(Guid.TryParse(call.Request.InteractionId, out _));
            Assert.Equal("line", call.Request.ChannelType);
            Assert.Equal(data.Destination, call.Request.ChannelInstanceId);
            Assert.Equal(data.ActorId, call.Request.ActorId);
            Assert.Equal(data.CharacterId, call.Request.CharacterId);
            Assert.False(string.IsNullOrEmpty(call.Request.InputMessageId));
            Assert.Equal(data.Text, call.Request.MessageText);
            Assert.NotNull(result.ReplyContext);
            Assert.Equal(data.EventId, result.ReplyContext.WebhookEventId);
            await Phase2ChatInputs.AssertReplyTokensAsync(result.ReplyContext, data.ReplyToken);
            ConnectionSamples.AssertRequests(host, external, new[] { external });
            Assert.Empty(outboundChat.Requests);
            Assert.Empty(outboundLine.Requests);
        }
        finally { await host.StopHostAsync(); }
    }

    [Theory(DisplayName = "C02-Internal-15: Contract processing forwards ready results and propagates failures")]
    [Trait("Case", "C02-Internal-15")]
    [InlineData("C02-Internal-15-01", "Ready passes input and token to Chat and replies once", "ready")]
    [InlineData("C02-Internal-15-03", "Failed assembly bypasses Chat and records failure", "failed")]
    [InlineData("C02-Internal-15-04", "Assembly exception reaches caller before Chat", "throw-assembly")]
    [InlineData("C02-Internal-15-05", "Chat exception reaches caller", "throw-chat")]
    public async Task ContractServices_PreparationOutcome_ControlsProcessing(string caseId, string description, string condition)
    {
        const string error = "assembly-failed-test";
        var data = Phase2FlowSamples.Random();
        var input = data.ChatV02();
        var context = await data.PrepareReplyContextAsync();
        var failure = new InvalidOperationException(condition);
        var expected = new ChatServiceResult { Status = "ok", Messages = [data.ReplyText] };
        var assembler = new InputAssemblerDouble<Request>((_, _) => condition switch
        {
            "throw-assembly" => Task.FromException<InputPreparationResult<Request>>(failure),
            "failed" => Task.FromResult(new InputPreparationResult<Request>(PreparationOutcome.Failed, null, null, error)),
            _ => Task.FromResult(new InputPreparationResult<Request>(PreparationOutcome.Ready, input, context, null))
        });
        var chat = new ChatBoundaryDouble<Request>((_, _) => condition == "throw-chat"
            ? Task.FromException<ChatServiceResult>(failure) : Task.FromResult(expected));
        var combination = new ContractServices<Request>(assembler, chat);
        var job = data.Job();
        using var token = new CancellationTokenSource(Limit);
        Trace(caseId, new { data, description, condition, error, ExpectedChat = condition is "ready" or "throw-chat" ? 1 : 0,
            ExpectedReplies = condition == "ready" ? 1 : 0, WaitSeconds = 10 });

        if (condition is "throw-assembly" or "throw-chat")
        {
            Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(
                () => combination.ProcessAsync(job, token.Token).WaitAsync(Limit)));
            Assert.Equal(condition == "throw-chat" ? 1 : 0, chat.Calls.Count);
            return;
        }

        var result = await combination.ProcessAsync(job, token.Token).WaitAsync(Limit);
        var reply = new ReplyBoundaryDouble();
        await using var rig = new Phase2WorkerRig(services =>
        {
            services.AddScoped<IContractServicesResolver>(_ => new ResolverDouble((_, _) =>
                Task.FromResult<IContractServices>(new ContractServicesDouble((_, _) => Task.FromResult(result)))));
            services.AddScoped<ILineReplyService>(_ => reply);
        });
        await rig.StartAsync().WaitAsync(Limit);
        var firstLog = rig.Logs.Entries.Count;
        await rig.Queue.DeliverAsync(job, token.Token).WaitAsync(Limit);
        if (condition == "ready")
        {
            var call = Assert.Single(chat.Calls);
            Assert.Same(input, call.Request);
            Assert.Equal(token.Token, call.Token);
            Assert.Equal(PreparationOutcome.Ready, result.Outcome);
            Assert.Same(expected, result.ChatResult);
            Assert.Equal(context, result.ReplyContext);
            Assert.Equal(expected.Messages, Assert.Single(reply.Calls).Messages);
        }
        else
        {
            Assert.Equal(PreparationOutcome.Failed, result.Outcome);
            Assert.Equal(error, result.ErrorMessage);
            Assert.Empty(chat.Calls);
            Assert.Empty(reply.Calls);
            var logs = rig.Logs.Entries.Skip(firstLog).ToArray();
            Trace(caseId + "-observed", new { Event = data.EventId, Logs = logs.Select(entry => new { entry.Category, entry.Level, entry.Message, entry.Values, Exception = entry.Exception?.ToString() }) });
            Assert.Contains(logs, entry => entry.Message.Contains(error, StringComparison.Ordinal)
                || entry.Values.Values.Any(value => value?.ToString()?.Contains(error, StringComparison.Ordinal) == true)
                || entry.Exception?.Message.Contains(error, StringComparison.Ordinal) == true);
        }
    }
}
#endif
