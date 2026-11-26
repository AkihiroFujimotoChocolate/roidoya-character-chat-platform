#if PHASE2_GATEWAY_TESTS
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

// Uses the real Program; the caller can supply explicit service overrides.
internal sealed class GatewayApplicationFactory : WebApplicationFactory<global::Program>
{
    internal CaptureHandler Chat { get; }
    internal CaptureHandler Line { get; }
    internal Dictionary<string, string?> Settings { get; } = new() {
        ["ContractVersion"] = "0.1",
        ["Contracts:C0_1:Chat:Fallbacks:Default"] = "test-default-fallback",
        ["Contracts:C0_1:Chat:Fallbacks:Timeout"] = "test-timeout-fallback",
        ["Chat:Http:BaseUrl"] = "https://chat.test/",
        ["App:BaseUrl"] = "https://app.test/",
        ["Chat:Http:Endpoint"] = "/chat/v0.1/generate-replies",
        ["Chat:RequestTimeoutSeconds"] = "1",
        ["ConnectionManagement:Http:BaseUrl"] = "https://connections.test/",
        ["Reply:QueueMaxSize"] = "15",
        ["Local:Queue:MaxSize"] = "15"
    };
    internal Uri ChatUri { get; set; } = new("https://chat.test/chat/v0.1/generate-replies");
    internal Uri GetUri { get; set; } = new("https://connections.test/channel/v0.1/get-connection");
    internal Uri ActorUri { get; set; } = new("https://connections.test/channel/v0.1/resolve-actor");
    internal bool UseEmptyQueueFullMessages { get; set; }
    internal CaptureHandler Connections { get; }
    internal CaptureHandler Unexpected { get; } = new((_, _) =>
        Task.FromException<HttpResponseMessage>(new InvalidOperationException("Unexpected outbound URI.")));
    private IHost? _host;
    private Task? _stopping;
    private readonly Action<IServiceCollection>? _configureTestServices;
    internal GatewayApplicationFactory(CaptureHandler chat, CaptureHandler line, CaptureHandler connections, Action<IServiceCollection>? configureTestServices = null)
    {
        _configureTestServices = configureTestServices;
        Chat = chat;
        Line = line;
        Connections = connections;
    }
    protected override IHost CreateHost(IHostBuilder builder)
    {
        // Minimal hosting reads startup settings before ConfigureAppConfiguration runs.
        // DeferredHostBuilder forwards host configuration as entry-point arguments.
        builder.UseEnvironment("Testing");
        builder.ConfigureHostConfiguration(config => config.AddInMemoryCollection(Settings));
        return _host = base.CreateHost(builder);
    }
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => {
            config.Sources.Clear();
            config.AddInMemoryCollection(Settings);
            if (UseEmptyQueueFullMessages)
                config.AddJsonStream(new MemoryStream("""{"Reply":{"QueueFullMessages":[]}}"""u8.ToArray()));
        });
        builder.ConfigureTestServices(services => {
            // Covers both named/typed and default HttpClient without naming a production service.
            services.ConfigureAll<HttpClientFactoryOptions>(options => {
                options.HttpClientActions.Add(client => client.Timeout = Timeout.InfiniteTimeSpan);
                options.HttpMessageHandlerBuilderActions.Add(b => b.PrimaryHandler = new Router(this));
            });
            _configureTestServices?.Invoke(services);
        });
    }
    internal Task StopHostAsync() => _stopping ??= StopAsync();

    private async Task StopAsync()
    {
        if (_host is null) return;
        using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await _host.StopAsync(limit.Token);
    }
    private sealed class Router(GatewayApplicationFactory host) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            var uri = request.RequestUri;
            var capture = uri == host.ChatUri ? host.Chat
                : uri == host.GetUri || uri == host.ActorUri ? host.Connections
                : uri?.Host == "api.line.me" ? host.Line : host.Unexpected;
            using var transport = new HttpMessageInvoker(capture, disposeHandler: false);
            return await transport.SendAsync(request, token);
        }
    }
}

#else
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


#endif

