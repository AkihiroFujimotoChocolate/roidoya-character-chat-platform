// SPDX-License-Identifier: MIT
using LineBot.Api.Services;
using LineBot.Api.Tests.Support;
using LineBot.Api.Tests.InternalContracts.Support;
using LineBot.Api.Tests.ExternalContracts.V01.Chat;
using LineBot.Api.Tests.ExternalContracts.V01.Line;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace LineBot.Api.Tests.InternalContracts.Composition.V01;

// The real Program registers the mode switch and hosted Worker. Only transport,
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
        ["Chat:Mode"] = "Http", ["Chat:ApiVersion"] = "0.1",
        ["Chat:Http:BaseUrl"] = "https://chat.test/", ["Chat:RequestTimeoutSeconds"] = "1",
        ["Line:ChannelSecret"] = LineWebhookSamples.Secret, ["Line:ChannelAccessToken"] = LineReplySamples.AccessToken,
        ["Reply:Qps"] = "100", ["Chat:Fallbacks:Default"] = ChatV01Samples.DefaultFallback,
        ["Chat:Fallbacks:Timeout"] = ChatV01Samples.TimeoutFallback
    };
    internal ProductionGatewayFactory(CaptureHandler? chat = null, CaptureHandler? line = null) {
        ChatHttp = chat ?? new((_, _) => Task.FromResult(ChatV01Samples.Ok("reply-a")));
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
            // The existing scoped HttpV01ChatService constructor accepts HttpClient.
            services.AddSingleton<HttpClient>(_ => ChatHttp.Client());
            services.AddHttpClient<ILineReplyService, LineReplyService>().ConfigurePrimaryHttpMessageHandler(() => LineHttp);
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
}
