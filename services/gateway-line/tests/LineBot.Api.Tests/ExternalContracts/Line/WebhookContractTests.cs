// SPDX-License-Identifier: MIT
using System.Net;
using System.Text;
using LineBot.Api.Tests.Support;
using Xunit.Abstractions;
namespace LineBot.Api.Tests.ExternalContracts.Line;

public sealed class WebhookContractTests : ExternalFlowTest
{
    public WebhookContractTests(ITestOutputHelper output) : base(output) { }

    [Theory, Trait("Case", "External-27")]
    [InlineData(false)] // External-27-01
    [InlineData(true)]  // External-27-02
    public async Task A_signed_text_event_reaches_both_outbound_http_boundaries(bool redelivery)
    {
        var data = ExternalSamples.Random(Samples.RichText);
        using var chat = new CaptureHandler((_, _) => Task.FromResult(Samples.Ok(data.ReplyText)));
        using var host = new ExternalGatewayFactory(chat);
        var body = data.Body(redelivery);
        Record(redelivery ? "External-27-02" : "External-27-01", host, data, body, Samples.Sign(body),
            new { Redelivery = redelivery, ChatStatus = 200, Messages = new[] { data.ReplyText }, LineStatus = 200 });
        using var client = host.CreateClient();
        await SendAsync(client, body, Samples.Sign(body));
        ExternalAssertions.Chat(await host.Chat.NextAsync(), data, "https://chat.test/chat/v0.1/generate-replies");
        ExternalAssertions.Line(await host.Line.NextAsync(), data, data.ReplyText);
        await FinishAsync(host, 1, 1);
    }

    [Theory, Trait("Case", "External-28")]
    [InlineData("missing")] // External-28-01
    [InlineData("wrong-secret")] // External-28-02
    [InlineData("changed-body")] // External-28-03
    public async Task Invalid_signatures_produce_no_outbound_http(string failure)
    {
        var data = ExternalSamples.Random(Samples.RichText);
        using var host = new ExternalGatewayFactory();
        var original = data.Body();
        var sent = failure == "changed-body" ? original.Concat(Encoding.UTF8.GetBytes(" ")).ToArray() : original;
        var signature = failure == "missing" ? null : Samples.Sign(original,
            failure == "wrong-secret" ? "another-synthetic-secret" : Samples.Secret);
        Record("External-28-0" + (failure == "missing" ? "1" : failure == "wrong-secret" ? "2" : "3"),
            host, data, sent, signature, new { Failure = failure,
                OriginalBodyBase64 = Convert.ToBase64String(original), ExpectedStatus = 400 });
        using var client = host.CreateClient();
        using var request = Samples.Incoming(sent, signature);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var response = await client.SendAsync(request, deadline.Token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await FinishAsync(host, 0, 0);
    }

    [Theory, Trait("Case", "External-29")]
    [InlineData("standby")] // External-29-01
    [InlineData("follow")] // External-29-02
    [InlineData("image")] // External-29-03
    public async Task Ignored_line_events_produce_no_outbound_http(string kind)
    {
        var data = ExternalSamples.Random();
        using var host = new ExternalGatewayFactory();
        var body = data.Body(mode: kind == "standby" ? "standby" : "active",
            kind: kind == "follow" ? "follow" : "message", messageKind: kind == "image" ? "image" : "text");
        Record("External-29-0" + (kind == "standby" ? "1" : kind == "follow" ? "2" : "3"),
            host, data, body, Samples.Sign(body), new { Kind = kind, ExpectedStatus = 200 });
        using var client = host.CreateClient();
        await SendAsync(client, body, Samples.Sign(body));
        await FinishAsync(host, 0, 0);
    }
}
