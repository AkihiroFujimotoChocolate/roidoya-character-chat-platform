#if PHASE2_GATEWAY_TESTS
// SPDX-License-Identifier: MIT
using System.Reflection;
using LineBot.Api.Chat;
using LineBot.Api.Processing;
using LineBot.Api.Queueing;
using LineBot.Api.Workers;
using LineBot.Api.Tests.ExternalContracts.V01.Line;
using LineBot.Api.Tests.ExternalContracts.V02.Chat;
using LineBot.Api.Tests.ExternalContracts.V02.Support;
using LineBot.Api.Tests.InternalContracts.Support;
using LineBot.Api.Tests.InternalContracts.Support.V02;
using LineBot.Api.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;
using Xunit.Abstractions;
using Request = LineBot.Api.Contracts.V0_2.ChatServiceRequest;

namespace LineBot.Api.Tests.InternalContracts.Composition;

public sealed class Phase2CompositionTests(ITestOutputHelper output) : DiagnosticTest(output)
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(10);

    [Fact(DisplayName = "C02-Internal-06-01: Production Resolver uses c0.2 keyed services and named HTTP client")]
    [Trait("Case", "C02-Internal-06")]
    public async Task Program_C02Registration_UsesMatchingNamedClient()
    {
        var data = Phase2FlowSamples.Random();
        var input = data.ChatV02();
        var outputId = GeneratedValues.Id("output-message");
        using var chat = new CaptureHandler((sent, _) => Task.FromResult(ChatV02Samples.AssertRequestAndCreateOk(sent, outputId, data.ReplyText)));
        using var unexpected = new CaptureHandler((_, _) => throw new InvalidOperationException("Unexpected named HTTP client."));
        using var host = new GatewayApplicationFactory(unexpected, unexpected, unexpected, services =>
            services.Configure<HttpClientFactoryOptions>("0.2", options =>
                options.HttpMessageHandlerBuilderActions.Add(builder => builder.PrimaryHandler = chat)));
        Trace("C02-Internal-06-01", new { data, host.Settings, outputId, Key = "0.2", ResponseStatus = 200, WaitSeconds = 10 });
        using var scope = host.Services.CreateScope();
        using var limit = new CancellationTokenSource(Limit);
        try
        {
            var resolver = scope.ServiceProvider.GetRequiredService<IContractServicesResolver>();
            var combination = Assert.IsType<ContractServices<Request>>(await resolver.ResolveAsync(data.Job(), limit.Token).WaitAsync(Limit));
            var fields = combination.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            var assembler = Assert.Single(fields.Where(field => typeof(IInputAssembler<Request>).IsAssignableFrom(field.FieldType))).GetValue(combination);
            var adapter = Assert.IsAssignableFrom<IChatService<Request>>(
                Assert.Single(fields.Where(field => typeof(IChatService<Request>).IsAssignableFrom(field.FieldType))).GetValue(combination));
            Assert.NotNull(assembler);
            Assert.Equal("LineBot.Api.Contracts.V0_2.InputAssembler", assembler.GetType().FullName);
            Assert.Equal("LineBot.Api.Contracts.V0_2.HttpChatService", adapter.GetType().FullName);
            Assert.Same(typeof(global::Program).Assembly, assembler.GetType().Assembly);
            Assert.Same(typeof(global::Program).Assembly, adapter.GetType().Assembly);
            await adapter.GenerateReplyAsync(input, limit.Token).WaitAsync(Limit);
            await chat.NextAsync();
            await Task.WhenAll(chat.AssertNoPendingRequestsAsync(), unexpected.AssertNoPendingRequestsAsync());
            await host.StopHostAsync();
            Assert.Single(chat.Requests);
            Assert.Empty(unexpected.Requests);
            Assert.Empty(host.Unexpected.Requests);
            Trace("C02-Internal-06-01-observed", new { Chat = chat.Requests });
        }
        finally { await host.StopHostAsync(); }
    }

    [Fact(DisplayName = "C02-Internal-06-03: Production service lifetimes hold across two scopes")]
    [Trait("Case", "C02-Internal-06")]
    public async Task Program_ProductionRegistration_PreservesLifetimes()
    {
        using var http = new CaptureHandler((_, _) => throw new InvalidOperationException("Unexpected HTTP request."));
        using var host = new GatewayApplicationFactory(http, http, http);
        Trace("C02-Internal-06-03", new { host.Settings, Scopes = 2, WorkerCount = 1, Key = "0.2" });
        using var first = host.Services.CreateScope();
        using var second = host.Services.CreateScope();
        try
        {
            var a = first.ServiceProvider;
            var b = second.ServiceProvider;
            var queue = a.GetRequiredService<ILineWebhookEventQueue>();
            Assert.Same(queue, a.GetRequiredService<ILineWebhookEventQueue>());
            Assert.Same(queue, b.GetRequiredService<ILineWebhookEventQueue>());
            Assert.Same(queue, b.GetRequiredService<ILineWebhookEventQueue>());
            Assert.Single(host.Services.GetServices<IHostedService>().OfType<MessageWorkerService>());
            foreach (var type in new[] { typeof(IContractServices), typeof(IInputAssembler<Request>), typeof(IChatService<Request>), typeof(HttpClient) })
            {
                var instance = a.GetRequiredKeyedService(type, "0.2");
                Assert.Same(instance, a.GetRequiredKeyedService(type, "0.2"));
                var other = b.GetRequiredKeyedService(type, "0.2");
                Assert.Same(other, b.GetRequiredKeyedService(type, "0.2"));
                Assert.NotSame(instance, other);
            }
        }
        finally { await host.StopHostAsync(); }
    }

    [Fact(DisplayName = "C02-Internal-06-05: Resolver asks its supplied provider for the selected key")]
    [Trait("Case", "C02-Internal-06")]
    public async Task Resolver_SuppliedProvider_ReturnsSameCombination()
    {
        var data = Phase2FlowSamples.Random();
        var service = new ContractServicesDouble((_, _) => throw new InvalidOperationException("Only resolution is expected."));
        var provider = new RecordingKeyedProvider(service);
        Trace("C02-Internal-06-05", new { data, ServiceType = nameof(IContractServices), Key = "0.2", WaitSeconds = 10 });
        using var limit = new CancellationTokenSource(Limit);
        Assert.Same(service, await new ContractServiceResolver("0.2", provider).ResolveAsync(data.Job(), limit.Token).WaitAsync(Limit));
        Assert.Equal((typeof(IContractServices), (object?)"0.2"), Assert.Single(provider.Calls));
    }

    private sealed class RecordingKeyedProvider(IContractServices combination) : IKeyedServiceProvider
    {
        internal List<(Type Type, object? Key)> Calls { get; } = new();
        public object? GetService(Type serviceType) => throw new InvalidOperationException("Expected keyed lookup.");
        public object? GetKeyedService(Type serviceType, object? serviceKey)
        {
            Calls.Add((serviceType, serviceKey));
            return serviceType == typeof(IContractServices) && Equals(serviceKey, "0.2") ? combination : null;
        }
        public object GetRequiredKeyedService(Type serviceType, object? serviceKey) =>
            GetKeyedService(serviceType, serviceKey) ?? throw new InvalidOperationException("Unexpected type or key.");
    }
}
#endif
