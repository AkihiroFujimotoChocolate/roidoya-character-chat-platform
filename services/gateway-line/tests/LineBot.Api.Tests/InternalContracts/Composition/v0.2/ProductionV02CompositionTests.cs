// SPDX-License-Identifier: MIT
using LineBot.Api.Services;
using LineBot.Api.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit.Abstractions;
namespace LineBot.Api.Tests.InternalContracts.Composition.V02;
public sealed class ProductionV02CompositionTests : DiagnosticTest
{
    public ProductionV02CompositionTests(ITestOutputHelper output) : base(output) { }
    [Theory(DisplayName = "C02-Internal-06: Production registration selects the requested Chat version")]
    [Trait("Case", "C02-Internal-06")]
    [InlineData("C02-Internal-06-01", "Version 0.1 selects the existing adapter without Character", "0.1", "HttpV01ChatService")]
    [InlineData("C02-Internal-06-02", "Version 0.2 selects the new adapter", "0.2", "HttpV02ChatService")]
    public async Task Program_ProductionRegistration_ResolvesSelectedChatService(
        string caseId, string caseDescription, string version, string expectedType)
    {
        using var factory = new ProductionGatewayFactory();
        factory.Settings["Chat:ApiVersion"] = version;
        if (version == "0.1") {
            factory.Settings["Chat:Http:Endpoint"] = "/chat/v0.1/generate-replies";
            factory.Settings.Remove("Character:Id");
        }
        Trace(caseId, new { FactorySettings = factory.Settings, ExpectedChatType = expectedType,
            ExpectedReplyType = "LineReplyService", ExpectedWorkerType = "MessageWorkerService", WaitLimitSeconds = 10 });
        _ = factory.Services;
        using var scope = factory.Services.CreateScope();
        var actual = scope.ServiceProvider.GetRequiredService<IChatService>();
        Assert.Equal("LineBot.Api.Services." + expectedType, actual.GetType().FullName);
        Assert.IsType<LineReplyService>(scope.ServiceProvider.GetRequiredService<ILineReplyService>());
        Assert.Contains(factory.Services.GetServices<IHostedService>(), s => s is MessageWorkerService);
        var assembly = typeof(IChatService).Assembly;
        foreach (var name in new[] { "HttpV01ChatService", "HttpV02ChatService", "MessageWorkerService", "LineReplyService" }) {
            var type = assembly.GetType("LineBot.Api.Services." + name);
            Assert.NotNull(type); Assert.True(type!.IsPublic); Assert.True(type.IsClass);
            Assert.Same(assembly, type.Assembly);
        }
        Trace(caseId + "-observed", new { ChatType = actual.GetType().FullName,
            Hosted = factory.Services.GetServices<IHostedService>().Select(x => x.GetType().FullName).ToArray() });
        await factory.StopWorkerAsync();
    }
}
