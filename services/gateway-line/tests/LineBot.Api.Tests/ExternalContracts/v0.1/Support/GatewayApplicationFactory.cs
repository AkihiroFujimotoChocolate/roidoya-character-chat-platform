// SPDX-License-Identifier: MIT
using LineBot.Api.Tests.Support;
using LineBot.Api.Tests.ExternalContracts.V01.Chat;
using LineBot.Api.Tests.ExternalContracts.V01.Line;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;
namespace LineBot.Api.Tests.ExternalContracts.V01.Support;

// Uses the real application and stores. Only HTTP transport and synthetic settings change.
internal sealed class GatewayApplicationFactory : WebApplicationFactory<global::Program>
{
    internal CaptureHandler Chat { get; }
    internal CaptureHandler Line { get; }
    internal Dictionary<string, string?> Settings { get; } = new() {
        ["Chat:ApiVersion"] = "0.1",
        ["Chat:Http:BaseUrl"] = "https://chat.test/",
        ["Chat:Http:Endpoint"] = "/chat/v0.1/generate-replies",
        ["Chat:RequestTimeoutSeconds"] = "1",
        ["Line:ChannelSecret"] = LineWebhookSamples.Secret,
        ["Line:ChannelAccessToken"] = LineReplySamples.AccessToken,
        ["Reply:Qps"] = "100",
        ["Chat:Fallbacks:Default"] = ChatV01Samples.DefaultFallback,
        ["Chat:Fallbacks:Timeout"] = ChatV01Samples.TimeoutFallback
    };
    private IHost? _host;
    internal GatewayApplicationFactory(CaptureHandler chat, CaptureHandler line)
    {
        Chat = chat;
        Line = line;
    }
    protected override IHost CreateHost(IHostBuilder builder) => _host = base.CreateHost(builder);
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => {
            config.Sources.Clear();
            config.AddInMemoryCollection(Settings);
        });
        builder.ConfigureTestServices(services => {
            // Covers both named/typed and default HttpClient without naming a production service.
            services.ConfigureAll<HttpClientFactoryOptions>(options => {
                options.HttpClientActions.Add(client => client.Timeout = Timeout.InfiniteTimeSpan);
                options.HttpMessageHandlerBuilderActions.Add(b => b.PrimaryHandler = new Router(Chat, Line));
            });
        });
    }
    internal async Task StopHostAsync()
    {
        if (_host is null) return;
        using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await _host.StopAsync(limit.Token);
    }
    private sealed class Router(CaptureHandler chat, CaptureHandler line) : HttpMessageHandler
    {
        private readonly HttpMessageInvoker _chat = new(chat, disposeHandler: false);
        private readonly HttpMessageInvoker _line = new(line, disposeHandler: false);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) =>
            (request.RequestUri?.Host == "api.line.me" ? _line : _chat).SendAsync(request, token);
        protected override void Dispose(bool disposing)
        {
            if (disposing) { _chat.Dispose(); _line.Dispose(); }
            base.Dispose(disposing);
        }
    }
}

