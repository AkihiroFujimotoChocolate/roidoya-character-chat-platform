// SPDX-License-Identifier: MIT
using System.Net;
using LineBot.Api.Tests.Support;
namespace LineBot.Api.Tests.ExternalContracts.Line;

public sealed class WebhookContractTests
{
    [Theory, Trait("Case", "External-27")]
    [InlineData(false)]
    [InlineData(true)]
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

    [Theory, Trait("Case", "External-28")]
    [InlineData("missing")]
    [InlineData("wrong-secret")]
    [InlineData("body-changed")]
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

    [Theory, Trait("Case", "External-29")]
    [InlineData("standby", "message", "text")]
    [InlineData("active", "follow", "text")]
    [InlineData("active", "message", "image")]
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
