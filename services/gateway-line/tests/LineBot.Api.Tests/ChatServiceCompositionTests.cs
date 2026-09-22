// SPDX-License-Identifier: MIT
using LineBot.Api.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace LineBot.Api.Tests;

public class ChatServiceCompositionTests
{
    [Fact]
    public async Task ModeHttp_ProductionCompositionResolvesHttpV01ChatService()
    {
        await using var factory = CreateFactory("Http");
        using var scope = factory.Services.CreateScope();

        var service = scope.ServiceProvider.GetRequiredService<IChatService>();

        Assert.IsType<HttpV01ChatService>(service);
    }

    [Fact]
    public async Task ModeEcho_ProductionCompositionResolvesEchoChatService()
    {
        await using var factory = CreateFactory("Echo");
        using var scope = factory.Services.CreateScope();

        var service = scope.ServiceProvider.GetRequiredService<IChatService>();

        Assert.IsType<EchoChatService>(service);
    }

    private static WebApplicationFactory<Program> CreateFactory(string mode)
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, configBuilder) =>
                {
                    configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Chat:Mode"] = mode,
                        ["Chat:ApiVersion"] = "0.1",
                        ["Chat:Http:BaseUrl"] = "https://example.com",
                        ["Line:ChannelSecret"] = "test-secret",
                        ["Line:ChannelAccessToken"] = "test-token"
                    });
                });

                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IHostedService>();
                });
            });
    }
}
