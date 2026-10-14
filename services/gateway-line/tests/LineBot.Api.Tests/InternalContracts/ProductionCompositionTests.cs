// SPDX-License-Identifier: MIT
using LineBot.Api.Services;
using LineBot.Api.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
namespace LineBot.Api.Tests.InternalContracts;

public sealed class ProductionCompositionTests
{
    [Fact, Trait("Case", "Internal-10")]
    public async Task Real_program_resolves_http_mode_through_chat_interface()
    {
        using var factory = new GatewayFactory();
        _ = factory.Services; // Boots the real entry point; no test-side mode switch.
        using var scope = factory.Services.CreateScope();
        Assert.IsType<HttpV01ChatService>(scope.ServiceProvider.GetRequiredService<IChatService>());
        Assert.Contains(factory.Services.GetServices<IHostedService>(), s => s is MessageWorkerService);
        await factory.StopWorkerAsync();
        // No singleton/scoped or HttpClient ownership requirement is introduced.
    }

    [Fact, Trait("Case", "Internal-11")]
    public async Task Real_worker_calls_replacements_registered_after_production_composition()
    {
        var chat = new ChatDouble(); var reply = new ReplyDouble();
        using var factory = new GatewayFactory { ChatOverride = chat, ReplyOverride = reply };
        _ = factory.Services;
        factory.Queue.TryEnqueue(Samples.Job());
        await factory.Queue.AfterJob.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await factory.StopWorkerAsync();
        Assert.Single(chat.Calls); Assert.Single(reply.Calls);
        Assert.Empty(factory.ChatHttp.Requests); Assert.Empty(factory.LineHttp.Requests);
    }
}
