// SPDX-License-Identifier: MIT
using LineBot.Api.Services;
using LineBot.Api.Tests.Support;
using LineBot.Api.Tests.InternalContracts.Support;
using LineBot.Api.Tests.InternalContracts.Composition.V02;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using FlowSamples = LineBot.Api.Tests.InternalContracts.Support.V02.FlowSamples;
using Xunit.Abstractions;

namespace LineBot.Api.Tests.InternalContracts.Composition;

public sealed class ProductionC02CompositionTests : DiagnosticTest
{
    public ProductionC02CompositionTests(ITestOutputHelper output) : base(output) { }

    [Fact(DisplayName = "C02-Internal-07: Worker uses abstract service replacements after production registration")]
    [Trait("Case", "C02-Internal-07")]
    public async Task Program_AbstractServiceOverrides_AreUsedByWorker()
    {
        // Arrange
        var data = FlowSamples.CreateRandom();
        var chat = new ChatDouble(); var reply = new ReplyDouble();
        using var factory = new ProductionGatewayFactory { ChatOverride = chat, ReplyOverride = reply };
        Trace("C02-Internal-07-01", new { Values = data, FactorySettings = factory.Settings,
            WaitLimitSeconds = 10, ExpectedChatCount = 1, ExpectedReplyCount = 1, ExpectedHttpCount = 0, ChatMessages = new[] { "selected-a", "selected-b" }, ChatStatus = "ok", ReplyResult = true });
        // Act
        _ = factory.Services;
        using var scope = factory.Services.CreateScope();
        Assert.Same(chat, scope.ServiceProvider.GetRequiredService<IChatService>());
        Assert.Same(reply, scope.ServiceProvider.GetRequiredService<ILineReplyService>());
        factory.Queue.TryEnqueue(data.Job());
        await factory.Queue.AfterJob.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await factory.StopWorkerAsync();
        // Assert
        Trace("C02-Internal-07-01-observed", new { ChatType = chat.GetType().FullName, ReplyType = reply.GetType().FullName, Chat = chat.Calls.Select(c => new { c.Request, CanBeCanceled = c.Token.CanBeCanceled, IsCancellationRequested = c.Token.IsCancellationRequested }).ToArray(), Reply = reply.Calls.Select(c => new { c.Token, c.Messages, c.EventId, c.Redelivery, CanBeCanceled = c.Cancellation.CanBeCanceled, IsCancellationRequested = c.Cancellation.IsCancellationRequested }).ToArray(), ChatHttpCount = factory.ChatHttp.Requests.Count, LineHttpCount = factory.LineHttp.Requests.Count });
        Assert.Single(chat.Calls); Assert.Single(reply.Calls);
        Assert.Empty(factory.ChatHttp.Requests); Assert.Empty(factory.LineHttp.Requests);
    }
}
