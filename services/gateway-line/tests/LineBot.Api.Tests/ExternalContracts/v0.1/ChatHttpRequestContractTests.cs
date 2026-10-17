// SPDX-License-Identifier: MIT
using LineBot.Api.Tests.Support;
using Xunit.Abstractions;
namespace LineBot.Api.Tests.ExternalContracts.V01;

public sealed class ChatHttpRequestContractTests : ExternalFlowTest
{
    public ChatHttpRequestContractTests(ITestOutputHelper output) : base(output) { }

    [Fact, Trait("Case", "External-01")]
    public async Task Webhook_input_becomes_the_v01_http_request()
    {
        var data = ExternalSamples.Random();
        using var chat = new CaptureHandler((_, _) => Task.FromResult(Samples.Ok(data.ReplyText)));
        using var host = new ExternalGatewayFactory(chat);
        var body = data.Body();
        Record("External-01-01", host, data, body, Samples.Sign(body),
            new { ChatStatus = 200, Messages = new[] { data.ReplyText }, LineStatus = 200 });
        using var client = host.CreateClient();
        await SendAsync(client, body, Samples.Sign(body));
        ExternalAssertions.Chat(await host.Chat.NextAsync(), data, "https://chat.test/chat/v0.1/generate-replies");
        ExternalAssertions.Line(await host.Line.NextAsync(), data, data.ReplyText);
        await FinishAsync(host, 1, 1);
    }

    [Theory, Trait("Case", "External-02")]
    [InlineData(false)] // External-02-01
    [InlineData(true)]  // External-02-02
    public async Task Chat_authentication_uses_only_the_configured_chat_key(bool useKey)
    {
        var data = ExternalSamples.Random();
        var key = useKey ? GeneratedValues.Id("chat-key") : null;
        using var chat = new CaptureHandler((_, _) => Task.FromResult(Samples.Ok(data.ReplyText)));
        using var host = new ExternalGatewayFactory(chat);
        host.Settings["Chat:Http:ApiKey"] = key;
        var body = data.Body();
        Record(useKey ? "External-02-02" : "External-02-01", host, data, body, Samples.Sign(body),
            new { ChatKey = key, ChatStatus = 200, Messages = new[] { data.ReplyText }, LineStatus = 200 });
        using var client = host.CreateClient();
        await SendAsync(client, body, Samples.Sign(body));
        var sent = await host.Chat.NextAsync();
        ExternalAssertions.Chat(sent, data, "https://chat.test/chat/v0.1/generate-replies", key);
        Assert.DoesNotContain(Samples.AccessToken, sent.Headers.Values.SelectMany(v => v));
        ExternalAssertions.Line(await host.Line.NextAsync(), data, data.ReplyText);
        await FinishAsync(host, 1, 1);
    }

    [Fact, Trait("Case", "External-14")]
    public async Task Webhook_special_characters_reach_chat_without_changes()
    {
        var data = ExternalSamples.Random(Samples.RichText);
        using var chat = new CaptureHandler((_, _) => Task.FromResult(Samples.Ok(data.ReplyText)));
        using var host = new ExternalGatewayFactory(chat);
        var body = data.Body();
        Record("External-14-01", host, data, body, Samples.Sign(body),
            new { ChatStatus = 200, Messages = new[] { data.ReplyText }, LineStatus = 200 });
        using var client = host.CreateClient();
        await SendAsync(client, body, Samples.Sign(body));
        ExternalAssertions.Chat(await host.Chat.NextAsync(), data, "https://chat.test/chat/v0.1/generate-replies");
        ExternalAssertions.Line(await host.Line.NextAsync(), data, data.ReplyText);
        await FinishAsync(host, 1, 1);
    }
}
