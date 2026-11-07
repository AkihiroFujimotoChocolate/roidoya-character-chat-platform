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
