// SPDX-License-Identifier: MIT
using LineBot.Api.Services;
using LineBot.Api.Tests.Support;
using LineBot.Api.Tests.InternalContracts.Support;
using LineBot.Api.Tests.ExternalContracts.V02.Chat;
using LineBot.Api.Tests.ExternalContracts.V01.Line;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace LineBot.Api.Tests.InternalContracts.Composition.V02;

// The real Program registers the selected Chat service and hosted Worker. Only transport,
// ancillary stores and (when requested) the abstract services are replaced.
internal sealed class ProductionGatewayFactory : WebApplicationFactory<global::Program>
{
    internal CaptureHandler ChatHttp { get; }
    internal CaptureHandler LineHttp { get; }
    internal LogBook Logs { get; } = new();
    internal ObservedQueue Queue { get; } = new();
    internal ChatDouble? ChatOverride { get; init; }
    internal ReplyDouble? ReplyOverride { get; init; }
    internal Dictionary<string, string?> Settings { get; } = new() {
        ["Chat:ApiVersion"] = "0.2",
        ["Chat:Http:Endpoint"] = "/chat/v0.2/generate-replies",
        ["Character:Id"] = "character-" + Guid.NewGuid().ToString("N"),
        ["Chat:Http:BaseUrl"] = "https://chat.test/", ["Chat:RequestTimeoutSeconds"] = "1",
        ["Line:ChannelSecret"] = LineWebhookSamples.Secret, ["Line:ChannelAccessToken"] = LineReplySamples.AccessToken,
        ["Reply:Qps"] = "100", ["Chat:Fallbacks:Default"] = ChatV02Samples.DefaultFallback,
        ["Chat:Fallbacks:Timeout"] = ChatV02Samples.TimeoutFallback
    };
    internal ProductionGatewayFactory(CaptureHandler? chat = null, CaptureHandler? line = null) {
        ChatHttp = chat ?? new((sent, _) => Task.FromResult(ChatV02Samples.Ok(sent, "output-" + Guid.NewGuid().ToString("N"), "reply-a")));
        LineHttp = line ?? new((_, _) => Task.FromResult(LineReplySamples.SuccessResponse()));
    }
    protected override void ConfigureWebHost(IWebHostBuilder builder) {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => {
            config.Sources.Clear(); // Do not inherit workstation credentials or routing.
            config.AddInMemoryCollection(Settings);
        });
        builder.ConfigureTestServices(services => {
            services.AddLogging(logging => logging.AddProvider(Logs));
            services.RemoveAll<ILocalQueueService>(); services.AddSingleton<ILocalQueueService>(Queue);
            services.RemoveAll<IUserLockService>(); services.AddSingleton<IUserLockService>(new TestLock());
            services.RemoveAll<IIdempotencyService>(); services.AddSingleton<IIdempotencyService>(new TestIdempotency());
            services.AddSingleton<HttpClient>(_ => ChatHttp.Client());
            services.ConfigureAll<HttpClientFactoryOptions>(options => {
                options.HttpMessageHandlerBuilderActions.Add(b => b.PrimaryHandler = new Router(ChatHttp, LineHttp));
            });
            if (ChatOverride is not null) { services.RemoveAll<IChatService>(); services.AddSingleton<IChatService>(ChatOverride); }
            if (ReplyOverride is not null) { services.RemoveAll<ILineReplyService>(); services.AddSingleton<ILineReplyService>(ReplyOverride); }
        });
    }
    internal async Task StopWorkerAsync() {
        var workers = Services.GetServices<IHostedService>().OfType<MessageWorkerService>().ToArray();
        foreach (var worker in workers) {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await worker.StopAsync(deadline.Token);
        }
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
