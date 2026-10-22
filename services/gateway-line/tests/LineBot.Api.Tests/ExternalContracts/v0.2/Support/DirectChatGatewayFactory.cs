// SPDX-License-Identifier: MIT
using LineBot.Api.Tests.Support;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;

namespace LineBot.Api.Tests.ExternalContracts.V02.Support;

// Starts the real entry point. Only settings and outbound HTTP transport change.
internal sealed class DirectChatGatewayFactory : WebApplicationFactory<global::Program>
{
    internal Dictionary<string, string?> Settings { get; }
    internal CaptureHandler Chat { get; }
    internal CaptureHandler Line { get; }
    private IHost? _host;
    internal DirectChatGatewayFactory(DirectChatValues values, CaptureHandler chat, CaptureHandler line)
    {
        Chat = chat; Line = line;
        Settings = new() {
            ["Chat:ApiVersion"] = "0.2", ["Chat:Http:BaseUrl"] = "https://chat.test/",
            ["Chat:Http:Endpoint"] = "/chat/v0.2/generate-replies", ["Chat:RequestTimeoutSeconds"] = "5",
            ["Character:Id"] = values.CharacterId,
            ["Line:ChannelSecret"] = DirectChatSamples.Secret, ["Line:ChannelAccessToken"] = DirectChatSamples.AccessToken,
            ["Reply:Qps"] = "100", ["Chat:Fallbacks:Default"] = "synthetic-system-failure",
            ["Chat:Fallbacks:Timeout"] = "synthetic-system-timeout"
        };
    }
    protected override IHost CreateHost(IHostBuilder builder) => _host = base.CreateHost(builder);
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        // Host settings are also available to early startup configuration.
        foreach (var setting in Settings) builder.UseSetting(setting.Key, setting.Value);
        builder.ConfigureAppConfiguration((_, config) => {
            config.Sources.Clear(); config.AddInMemoryCollection(Settings);
        });
        builder.ConfigureTestServices(services => {
            services.ConfigureAll<HttpClientFactoryOptions>(options => {
                options.HttpClientActions.Add(client => client.Timeout = Timeout.InfiniteTimeSpan);
                options.HttpMessageHandlerBuilderActions.Add(b => b.PrimaryHandler = new Router(Chat, Line));
            });
        });
    }
    internal async Task StopAsync()
    {
        if (_host is null) return;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await _host.StopAsync(deadline.Token);
    }
    private sealed class Router(CaptureHandler chat, CaptureHandler line) : HttpMessageHandler
    {
        private readonly HttpMessageInvoker _chat = new(chat, false);
        private readonly HttpMessageInvoker _line = new(line, false);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) =>
            (request.RequestUri?.Host == "api.line.me" ? _line : _chat).SendAsync(request, token);
        protected override void Dispose(bool disposing)
        {
            if (disposing) { _chat.Dispose(); _line.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
