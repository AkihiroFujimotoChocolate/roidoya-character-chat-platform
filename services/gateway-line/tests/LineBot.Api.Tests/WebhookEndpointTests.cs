// SPDX-License-Identifier: MIT
using System.Net;
using System.Security.Cryptography;
using System.Text;
using LineBot.Api.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace LineBot.Api.Tests;

public class WebhookEndpointTests
{
    private const string ChannelSecret = "test-channel-secret";

    [Fact]
    public async Task Webhook_AcceptsCorrectSignatureAndEnqueuesRepresentativeTextEvent()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();

        var body = BuildRepresentativeWebhookJson();
        var request = new HttpRequestMessage(HttpMethod.Post, "/line/webhook")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Line-Signature", CreateSignature(ChannelSecret, body));
        request.Headers.Add("X-Line-Request-Id", "request-1");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var queue = scope.ServiceProvider.GetRequiredService<ILocalQueueService>();
        var item = queue.TryDequeue();
        Assert.NotNull(item);
        Assert.Equal("webhook-event-1", item!.WebhookEventId);
        Assert.Equal("reply-token-1", item.ReplyToken);
        Assert.Equal("user-U1234567890", item.UserKey);
        Assert.Equal("hello from line", item.MessageText);
        Assert.False(item.IsRedelivery);
    }

    [Fact]
    public async Task Webhook_RejectsMissingSignature()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsync("/line/webhook", new StringContent(BuildRepresentativeWebhookJson(), Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Webhook_RejectsInvalidSignature()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Post, "/line/webhook")
        {
            Content = new StringContent(BuildRepresentativeWebhookJson(), Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Line-Signature", Convert.ToBase64String(Encoding.UTF8.GetBytes("invalid")));

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static WebApplicationFactory<Program> CreateFactory()
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, configBuilder) =>
                {
                    configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Line:ChannelSecret"] = ChannelSecret,
                        ["Line:ChannelAccessToken"] = "token-for-tests",
                        ["Chat:Mode"] = "Echo"
                    });
                });

                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IHostedService>();
                });
            });
    }

    private static string BuildRepresentativeWebhookJson()
    {
        return """
               {
                 "destination": "Udestination",
                 "events": [
                   {
                     "type": "message",
                     "mode": "active",
                     "timestamp": 1716100000000,
                     "webhookEventId": "webhook-event-1",
                     "deliveryContext": { "isRedelivery": false },
                     "source": {
                       "type": "user",
                       "userId": "U1234567890"
                     },
                     "replyToken": "reply-token-1",
                     "message": {
                       "id": "message-id-1",
                       "type": "text",
                       "text": "hello from line"
                     }
                   }
                 ]
               }
               """;
    }

    private static string CreateSignature(string channelSecret, string requestBody)
    {
        var secretBytes = Encoding.UTF8.GetBytes(channelSecret);
        var bodyBytes = Encoding.UTF8.GetBytes(requestBody);
        using var hmac = new HMACSHA256(secretBytes);
        return Convert.ToBase64String(hmac.ComputeHash(bodyBytes));
    }
}
