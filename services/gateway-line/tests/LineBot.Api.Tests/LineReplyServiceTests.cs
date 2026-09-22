// SPDX-License-Identifier: MIT
using System.Net;
using System.Text;
using System.Text.Json;
using LineBot.Api.Services;

namespace LineBot.Api.Tests;

public class LineReplyServiceTests
{
    [Fact]
    public async Task SendReplyAsync_UsesExpectedHttpContractAndReturnsTrueOnSuccess()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        }));

        var service = new LineReplyService(
            new HttpClient(handler),
            TestSupport.BuildConfiguration(new Dictionary<string, string?> { ["Line:ChannelAccessToken"] = "token" }),
            TestSupport.Logger<LineReplyService>());

        var result = await service.SendReplyAsync("reply-token", new List<string> { "hello" }, "webhook-1", false, CancellationToken.None);

        Assert.True(result);
        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("https://api.line.me/v2/bot/message/reply", handler.LastRequest.RequestUri!.ToString());
        Assert.Equal("token", handler.LastRequest.Headers.Authorization!.Parameter);

        var payload = await handler.LastRequest.Content!.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(payload);
        Assert.Equal("reply-token", doc.RootElement.GetProperty("replyToken").GetString());
        var messages = doc.RootElement.GetProperty("messages");
        Assert.Single(messages.EnumerateArray());
        var message = messages[0];
        Assert.Equal("text", message.GetProperty("type").GetString());
        Assert.Equal("hello", message.GetProperty("text").GetString());
    }

    [Fact]
    public async Task SendReplyAsync_ReturnsFalseOnNonSuccess()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("bad request", Encoding.UTF8, "text/plain")
        }));

        var service = new LineReplyService(
            new HttpClient(handler),
            TestSupport.BuildConfiguration(new Dictionary<string, string?> { ["Line:ChannelAccessToken"] = "token" }),
            TestSupport.Logger<LineReplyService>());

        var result = await service.SendReplyAsync("reply-token", new List<string> { "hello" }, "webhook-1", true, CancellationToken.None);

        Assert.False(result);
    }
}
