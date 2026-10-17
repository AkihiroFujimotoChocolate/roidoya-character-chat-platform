// SPDX-License-Identifier: MIT
using LineBot.Api.Tests.Support;
using Xunit.Abstractions;
namespace LineBot.Api.Tests.ExternalContracts.V01;

public sealed class ChatRoutingContractTests : ExternalFlowTest
{
    public ChatRoutingContractTests(ITestOutputHelper output) : base(output) { }

    [Theory, Trait("Case", "External-06")]
    [InlineData("/api/chat/v0.1")] // External-06-01
    [InlineData("/chat/v0.1/generate-replies")] // External-06-02
    public async Task Webhook_uses_the_configured_connection_endpoint(string endpoint)
    {
        var data = ExternalSamples.Random();
        using var chat = new CaptureHandler((_, _) => Task.FromResult(Samples.Ok(data.ReplyText)));
        using var host = new ExternalGatewayFactory(chat);
        host.Settings["Chat:Http:Endpoint"] = endpoint;
        host.Settings["Chat:Http:EndpointTemplate"] = "/unused/{version}/reply";
        var body = data.Body();
        Record(endpoint == "/api/chat/v0.1" ? "External-06-01" : "External-06-02",
            host, data, body, Samples.Sign(body),
            new { ChatStatus = 200, Messages = new[] { data.ReplyText }, LineStatus = 200 });
        using var client = host.CreateClient();
        await SendAsync(client, body, Samples.Sign(body));
        ExternalAssertions.Chat(await host.Chat.NextAsync(), data, "https://chat.test" + endpoint);
        ExternalAssertions.Line(await host.Line.NextAsync(), data, data.ReplyText);
        await FinishAsync(host, 1, 1);
    }
}
