// SPDX-License-Identifier: MIT
using System.Net;
using LineBot.Api.Tests.Support;
namespace LineBot.Api.Tests.ExternalContracts.Line;

public sealed class WebhookContractTests
{
    // External-27: Verifies signed direct text preserves connection values and redelivery through the real host.
    [Theory, Trait("Case", "External-27")]
    [InlineData(false)] // External-27-01: first delivery
    [InlineData(true)]  // External-27-02: redelivery
    public async Task Signed_direct_text_keeps_existing_connection_values(bool redelivery)
    {
        using var factory = new GatewayFactory(); using var client = factory.CreateClient();
        var bytes = Samples.Webhook(redelivery, text: Samples.RichText);
        using var request = Samples.Incoming(bytes, Samples.Sign(bytes));
        using var ack = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, ack.StatusCode);
        var accepted = await factory.Queue.Accepted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var chat = await factory.ChatHttp.NextAsync();
        var line = await factory.LineHttp.NextAsync();
        await factory.Queue.AfterJob.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await factory.StopWorkerAsync();
        Assert.Equal("event-test", accepted.WebhookEventId);
        Assert.Equal(redelivery, accepted.IsRedelivery);
        Assert.Equal("reply-test", accepted.ReplyToken);
        using (var json = chat.Json()) {
            var body = json.RootElement;
            Assert.Equal("line", body.GetProperty("origin").GetProperty("platform").GetString());
            Assert.Equal("user-person-test", body.GetProperty("conversation").GetProperty("id").GetString());
            Assert.Equal("line-user-person-test", body.GetProperty("author").GetProperty("user_id").GetString());
            Assert.Equal(Samples.RichText, body.GetProperty("message").GetProperty("text").GetString());
        }
        using (var json = line.Json()) Assert.Equal("reply-test", json.RootElement.GetProperty("replyToken").GetString());
        Assert.Single(factory.ChatHttp.Requests); Assert.Single(factory.LineHttp.Requests);
    }

    // External-28: Verifies invalid signatures reject events without queuing or outgoing HTTP.
    [Theory, Trait("Case", "External-28")]
    [InlineData("missing")]      // External-28-01: signature missing
    [InlineData("wrong-secret")] // External-28-02: signature made with another secret
    [InlineData("body-changed")] // External-28-03: body bytes changed after signing
    public async Task Invalid_signature_does_not_process_any_event(string condition)
    {
        using var factory = new GatewayFactory(); using var client = factory.CreateClient();
        var original = Samples.Webhook(text: Samples.RichText);
        string? signature = condition == "missing" ? null :
            Samples.Sign(original, condition == "wrong-secret" ? "other-test-secret" : Samples.Secret);
        // Appending JSON whitespace changes signed bytes without invalidating JSON.
        var sent = condition == "body-changed" ? original.Concat(new byte[] { 0x20 }).ToArray() : original;
        using var request = Samples.Incoming(sent, signature);
        using var ack = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, ack.StatusCode);
        await factory.StopWorkerAsync();
        Assert.Equal(0, factory.Queue.AcceptedCount);
        Assert.Empty(factory.ChatHttp.Requests); Assert.Empty(factory.LineHttp.Requests);
    }

    // External-29: Verifies regular events outside the processing scope are acknowledged without processing.
    [Theory, Trait("Case", "External-29")]
    [InlineData("standby", "message", "text")] // External-29-01: standby text message
    [InlineData("active", "follow", "text")]   // External-29-02: active follow event
    [InlineData("active", "message", "image")] // External-29-03: active image message
    public async Task Out_of_scope_regular_event_is_acknowledged_without_processing(string mode, string kind, string messageKind)
    {
        using var factory = new GatewayFactory(); using var client = factory.CreateClient();
        var bytes = Samples.Webhook(mode: mode, kind: kind, messageKind: messageKind);
        using var request = Samples.Incoming(bytes, Samples.Sign(bytes));
        using var ack = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, ack.StatusCode);
        await factory.StopWorkerAsync();
        Assert.Equal(0, factory.Queue.AcceptedCount);
        Assert.Empty(factory.ChatHttp.Requests); Assert.Empty(factory.LineHttp.Requests);
    }
}
