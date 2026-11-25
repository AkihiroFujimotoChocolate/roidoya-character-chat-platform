#if PHASE2_GATEWAY_TESTS
// SPDX-License-Identifier: MIT
using LineBot.Api.Chat;
using LineBot.Api.Processing;
using LineBot.Api.Tests.ExternalContracts.V02.Chat;
using LineBot.Api.Tests.ExternalContracts.V02.Support;
using LineBot.Api.Tests.InternalContracts.Support;
using LineBot.Api.Tests.InternalContracts.Support.V02;
using LineBot.Api.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;
using Request = LineBot.Api.Contracts.V0_2.ChatServiceRequest;

namespace LineBot.Api.Tests.InternalContracts.Boundaries.V02;

public sealed class ChatV02AdapterTests(ITestOutputHelper output) : DiagnosticTest(output)
{
    [Fact(DisplayName = "C02-Internal-03-01: Supplied IDs reach HTTP and response text is preserved")]
    [Trait("Case", "C02-Internal-03")]
    public async Task Chat_SuppliedIdsAndNormalResponse_PreserveBoundaryValues()
    {
        var data = Phase2FlowSamples.Random();
        var outputId = GeneratedValues.Id("output-message");
        var text = data.ReplyText;
        var request = data.ChatV02();
        using var httpResponse = ChatV02Samples.Ok(data.InteractionId, outputId, text);
        var responseBody = await httpResponse.Content.ReadAsStringAsync();
        using var chat = new CaptureHandler((_, _) => Task.FromResult(ChatV02Samples.Ok(data.InteractionId, outputId, text)));
        using var other = new CaptureHandler((_, _) => throw new InvalidOperationException("Unexpected outbound request."));
        using var host = new GatewayApplicationFactory(chat, other, other);
        Trace("C02-Internal-03-01", new { host.Settings, request, ResponseBody = responseBody,
            ResponseStatus = 200, ExpectedInteractionId = data.InteractionId, ExpectedMessageId = data.InputMessageId,
            ExpectedMessages = new[] { text }, WaitLimitSeconds = 10 });
        using var scope = host.Services.CreateScope();
        using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            var result = await scope.ServiceProvider.GetRequiredKeyedService<IChatService<Request>>("0.2")
                .GenerateReplyAsync(request, limit.Token).WaitAsync(TimeSpan.FromSeconds(10));
            var sent = await chat.NextAsync();
            using var json = sent.Json();
            Assert.Equal(data.InteractionId, json.RootElement.GetProperty("interaction_id").GetString());
            Assert.Equal(data.InputMessageId, json.RootElement.GetProperty("message").GetProperty("id").GetString());
            Assert.Equal(new[] { text }, result.Messages);
            await chat.AssertNoPendingRequestsAsync();
            await host.StopHostAsync();
            Assert.Empty(other.Requests);
            Assert.Empty(host.Unexpected.Requests);
            Assert.Single(chat.Requests);
            Trace("C02-Internal-03-01-observed", new { Request = sent, ResponseBody = responseBody, Result = result });
        }
        finally { await host.StopHostAsync(); }
    }
}
#else
// SPDX-License-Identifier: MIT
using LineBot.Api.Models;
using LineBot.Api.Services;
using LineBot.Api.Tests.Support;
using LineBot.Api.Tests.InternalContracts.Composition.V02;
using LineBot.Api.Tests.InternalContracts.Support.V02;
using LineBot.Api.Tests.ExternalContracts.V02.Chat;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;
namespace LineBot.Api.Tests.InternalContracts.Boundaries.V02;
public sealed class ChatV02AdapterTests : DiagnosticTest
{
    public ChatV02AdapterTests(ITestOutputHelper output) : base(output) { }
    [Fact(DisplayName = "C02-Internal-03: Supplied input IDs reach HTTP and normal text becomes the internal result")]
    [Trait("Case", "C02-Internal-03")]
    public async Task Chat_SuppliedIdsAndNormalResponse_PreserveBoundaryValues()
    {
        var data = FlowSamples.CreateRandom();
        var outputId = "output-message-" + Guid.NewGuid().ToString("N");
        var text = data.Messages[0];
        var request = new ChatServiceRequest { MessageText = data.Text };
        foreach (var name in new[] { "InteractionId", "ChannelType", "ChannelInstanceId", "ActorId", "CharacterId", "InputMessageId" })
            typeof(ChatServiceRequest).GetProperty(name)!.SetValue(request, data.GetType().GetProperty(name)!.GetValue(data));
        using var httpResponse = ChatV02Samples.Ok(data.InteractionId, outputId, text);
        var responseBody = await httpResponse.Content.ReadAsStringAsync();
        using var chatHttp = new CaptureHandler((_, _) => Task.FromResult(ChatV02Samples.Ok(data.InteractionId, outputId, text)));
        using var factory = new ProductionGatewayFactory(chatHttp);
        Trace("C02-Internal-03-01", new { FactorySettings = factory.Settings, Request = request,
            ResponseBody = responseBody, ResponseStatus = 200, CorrelationRule = "use supplied InteractionId",
            ExpectedInteractionId = data.InteractionId, ExpectedMessageId = data.InputMessageId,
            ExpectedMessages = new[] { text }, WaitLimitSeconds = 10 });
        using var scope = factory.Services.CreateScope();
        using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var result = await scope.ServiceProvider.GetRequiredService<IChatService>().GenerateReplyAsync(request, limit.Token);
        var sent = await chatHttp.NextAsync();
        using var json = sent.Json();
        Assert.Equal(data.InteractionId, json.RootElement.GetProperty("interaction_id").GetString());
        Assert.Equal(data.InputMessageId, json.RootElement.GetProperty("message").GetProperty("id").GetString());
        Assert.Equal(new[] { text }, result.Messages);
        Trace("C02-Internal-03-01-observed", new { Request = sent, ResponseBody = responseBody, Result = result });
        await factory.StopWorkerAsync();
    }
}
#endif
