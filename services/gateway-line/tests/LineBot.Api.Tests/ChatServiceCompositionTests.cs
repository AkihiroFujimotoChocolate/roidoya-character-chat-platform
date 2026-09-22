// SPDX-License-Identifier: MIT
using LineBot.Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LineBot.Api.Tests;

public class ChatServiceCompositionTests
{
    [Fact]
    public void ModeHttp_ResolvesHttpV01ChatService()
    {
        using var provider = BuildServiceProvider("Http");

        var service = provider.GetRequiredService<IChatService>();

        Assert.IsType<HttpV01ChatService>(service);
    }

    [Fact]
    public void ModeEcho_ResolvesEchoChatService()
    {
        using var provider = BuildServiceProvider("Echo");

        var service = provider.GetRequiredService<IChatService>();

        Assert.IsType<EchoChatService>(service);
    }

    private static ServiceProvider BuildServiceProvider(string mode)
    {
        var services = new ServiceCollection();
        var config = TestSupport.BuildConfiguration(new Dictionary<string, string?>
        {
            ["Chat:Mode"] = mode,
            ["Chat:ApiVersion"] = "0.1",
            ["Chat:Http:BaseUrl"] = "https://example.com",
            ["Line:ChannelAccessToken"] = "token"
        });

        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();
        services.AddHttpClient<HttpV01ChatService>();
        services.AddScoped<EchoChatService>();
        services.AddScoped<HttpV01ChatService>();

        services.AddScoped<IChatService>(sp =>
        {
            var configuration = sp.GetRequiredService<IConfiguration>();
            var chatMode = configuration.GetValue<string>("Chat:Mode", "Echo");

            return (chatMode ?? "Echo").ToLowerInvariant() switch
            {
                "http" => (IChatService)sp.GetRequiredService<HttpV01ChatService>(),
                "echo" => sp.GetRequiredService<EchoChatService>(),
                _ => throw new InvalidOperationException($"Unsupported Chat:Mode: {chatMode}")
            };
        });

        return services.BuildServiceProvider();
    }
}
