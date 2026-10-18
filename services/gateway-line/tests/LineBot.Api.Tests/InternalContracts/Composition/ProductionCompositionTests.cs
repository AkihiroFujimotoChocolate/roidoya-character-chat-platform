// SPDX-License-Identifier: MIT
using LineBot.Api.Services;
using LineBot.Api.Tests.Support;
using LineBot.Api.Tests.InternalContracts.Support;
using LineBot.Api.Tests.InternalContracts.Composition.V01;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit.Abstractions;

namespace LineBot.Api.Tests.InternalContracts.Composition;

public sealed class ProductionCompositionTests : DiagnosticTest
{
    public ProductionCompositionTests(ITestOutputHelper output) : base(output) { }

    [Fact(DisplayName = "Internal-11: Worker uses abstract service replacements after production registration")]
    [Trait("Case", "Internal-11")]
    public async Task Program_AbstractServiceOverrides_AreUsedByWorker()
    {
        // Arrange
        var data = FlowSamples.CreateRandom();
        var chat = new ChatDouble(); var reply = new ReplyDouble();
        using var factory = new ProductionGatewayFactory { ChatOverride = chat, ReplyOverride = reply };
        Trace("Internal-11-01", new { Values = data, FactorySettings = factory.Settings,
            ChatMessages = new[] { "selected-a", "selected-b" }, ChatStatus = "ok", ReplyResult = true });
        // Act
        _ = factory.Services;
        factory.Queue.TryEnqueue(data.Job());
        await factory.Queue.AfterJob.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await factory.StopWorkerAsync();
        // Assert
        Assert.Single(chat.Calls); Assert.Single(reply.Calls);
        Assert.Empty(factory.ChatHttp.Requests); Assert.Empty(factory.LineHttp.Requests);
    }
}
