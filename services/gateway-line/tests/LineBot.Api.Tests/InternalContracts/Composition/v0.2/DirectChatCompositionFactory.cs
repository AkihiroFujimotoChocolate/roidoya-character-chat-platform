// SPDX-License-Identifier: MIT
using LineBot.Api.Services;
using LineBot.Api.Tests.InternalContracts.Support;
using LineBot.Api.Tests.Support;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;

namespace LineBot.Api.Tests.InternalContracts.Composition.V02;

internal sealed class DirectChatCompositionFactory : WebApplicationFactory<global::Program>
{
    internal ObservedQueue Queue { get; } = new();
    internal ChatDouble? ChatOverride { get; init; }
    internal ReplyDouble? ReplyOverride { get; init; }
    internal Dictionary<string, string?> Settings { get; } = new() {
        ["Chat:ApiVersion"] = "0.2", ["Chat:Http:BaseUrl"] = "https://chat.test/",
        ["Chat:Http:Endpoint"] = "/chat/v0.2/generate-replies", ["Character:Id"] = "character:internal-test",
        ["Line:ChannelSecret"] = "synthetic-internal-secret", ["Line:ChannelAccessToken"] = "synthetic-internal-token",
        ["Reply:Qps"] = "100", ["Chat:RequestTimeoutSeconds"] = "5"
    };
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        foreach (var setting in Settings) builder.UseSetting(setting.Key, setting.Value);
        builder.ConfigureAppConfiguration((_, config) => { config.Sources.Clear(); config.AddInMemoryCollection(Settings); });
        builder.ConfigureTestServices(services => {
            services.RemoveAll<ILocalQueueService>(); services.AddSingleton<ILocalQueueService>(Queue);
            services.RemoveAll<IUserLockService>(); services.AddSingleton<IUserLockService>(new TestLock());
            services.RemoveAll<IIdempotencyService>(); services.AddSingleton<IIdempotencyService>(new TestIdempotency());
            services.ConfigureAll<HttpClientFactoryOptions>(options => {
                options.HttpMessageHandlerBuilderActions.Add(b => b.PrimaryHandler = new RejectUnexpectedHttp());
            });
            if (ChatOverride is not null) { services.RemoveAll<IChatService>(); services.AddSingleton<IChatService>(ChatOverride); }
            if (ReplyOverride is not null) { services.RemoveAll<ILineReplyService>(); services.AddSingleton<ILineReplyService>(ReplyOverride); }
        });
    }
    internal async Task StopWorkerAsync()
    {
        foreach (var worker in Services.GetServices<IHostedService>().OfType<MessageWorkerService>()) {
            using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await worker.StopAsync(limit.Token);
        }
    }
    private sealed class RejectUnexpectedHttp : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromException<HttpResponseMessage>(new InvalidOperationException("This internal test must not use HTTP."));
    }
}
