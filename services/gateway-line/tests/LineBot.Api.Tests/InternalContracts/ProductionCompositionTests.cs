// SPDX-License-Identifier: MIT
using LineBot.Api.Services;
using LineBot.Api.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit.Abstractions;
namespace LineBot.Api.Tests.InternalContracts;

public sealed class ProductionCompositionTests : DiagnosticTest
{
    public ProductionCompositionTests(ITestOutputHelper output) : base(output) { }

    // Internal-10 / Internal-10-01: Verifies the real Program resolves Http mode through IChatService and registers Worker.
    [Fact, Trait("Case", "Internal-10")]
    public async Task Real_program_resolves_http_mode_through_chat_interface()
    {
        using var factory = new GatewayFactory();
        Trace("Internal-10-01", new { FactorySettings = factory.Settings });
        _ = factory.Services; // Boots the real entry point; no test-side mode switch.
        using var scope = factory.Services.CreateScope();
        Assert.IsType<HttpV01ChatService>(scope.ServiceProvider.GetRequiredService<IChatService>());
        Assert.Contains(factory.Services.GetServices<IHostedService>(), s => s is MessageWorkerService);
        await factory.StopWorkerAsync();
        // No singleton/scoped or HttpClient ownership requirement is introduced.
    }

    // Internal-11 / Internal-11-01: Verifies the real Worker uses abstract service replacements after production registration.
    [Fact, Trait("Case", "Internal-11")]
    public async Task Real_worker_calls_replacements_registered_after_production_composition()
    {
        var data = FlowSamples.CreateRandom();
        var chat = new ChatDouble(); var reply = new ReplyDouble();
        using var factory = new GatewayFactory { ChatOverride = chat, ReplyOverride = reply };
        Trace("Internal-11-01", new { Values = data, FactorySettings = factory.Settings,
            ChatMessages = new[] { "selected-a", "selected-b" }, ChatStatus = "ok", ReplyResult = true });
        _ = factory.Services;
        factory.Queue.TryEnqueue(data.Job());
        await factory.Queue.AfterJob.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await factory.StopWorkerAsync();
        Assert.Single(chat.Calls); Assert.Single(reply.Calls);
        Assert.Empty(factory.ChatHttp.Requests); Assert.Empty(factory.LineHttp.Requests);
    }
}
