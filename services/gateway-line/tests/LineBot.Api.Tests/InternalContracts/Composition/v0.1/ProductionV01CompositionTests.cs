// SPDX-License-Identifier: MIT
using LineBot.Api.Services;
using LineBot.Api.Tests.Support;
using LineBot.Api.Tests.InternalContracts.Support;
using LineBot.Api.Tests.InternalContracts.Composition.V01;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit.Abstractions;

namespace LineBot.Api.Tests.InternalContracts.Composition.V01;

public sealed class ProductionV01CompositionTests : DiagnosticTest
{
    public ProductionV01CompositionTests(ITestOutputHelper output) : base(output) { }

    [Fact(DisplayName = "Internal-10: Production Http mode resolves the v0.1 Chat service and registers Worker")]
    [Trait("Case", "Internal-10")]
    public async Task Program_HttpMode_ResolvesV01ChatService()
    {
        // Arrange
        using var factory = new ProductionGatewayFactory();
        Trace("Internal-10-01", new { FactorySettings = factory.Settings });
        // Act
        _ = factory.Services; // Boots the real entry point; no test-side mode switch.
        using var scope = factory.Services.CreateScope();
        // Assert
        Assert.IsType<HttpV01ChatService>(scope.ServiceProvider.GetRequiredService<IChatService>());
        Assert.Contains(factory.Services.GetServices<IHostedService>(), s => s is MessageWorkerService);
        await factory.StopWorkerAsync();
        // No singleton/scoped or HttpClient ownership requirement is introduced.
    }

}
