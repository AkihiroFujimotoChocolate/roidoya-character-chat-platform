// SPDX-License-Identifier: MIT
using LineBot.Api.Models;
using LineBot.Api.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LineBot.Api.Tests;

public class MessageWorkerServiceTests
{
    [Fact]
    public async Task Worker_UsesSemanticChatRequestWithoutApiVersion()
    {
        var queue = new StubQueueService();
        queue.Enqueue(new QueueItem
        {
            WebhookEventId = "event-1",
            ReplyToken = "token-1",
            UserKey = "user-1",
            MessageText = "hello",
            IsRedelivery = false
        });

        var chat = new CapturingChatService();
        var reply = new CapturingLineReplyService();

        var services = new ServiceCollection()
            .AddSingleton<ILocalQueueService>(queue)
            .AddSingleton<IUserLockService, StubUserLockService>()
            .AddSingleton<IChatService>(chat)
            .AddSingleton<ILineReplyService>(reply)
            .AddSingleton<IIdempotencyService, StubIdempotencyService>()
            .BuildServiceProvider();

        var configuration = TestSupport.BuildConfiguration(new Dictionary<string, string?>
        {
            ["Reply:Qps"] = "100"
        });

        var worker = new MessageWorkerService(services, TestSupport.Logger<MessageWorkerService>(), configuration);

        await worker.StartAsync(CancellationToken.None);
        await chat.WaitForInvocationAsync(TimeSpan.FromSeconds(2));
        await worker.StopAsync(CancellationToken.None);

        Assert.NotNull(chat.LastRequest);
        Assert.Equal("hello", chat.LastRequest!.MessageText);
        Assert.Equal("user-1", chat.LastRequest.ConversationId);
        Assert.Equal("user-1", chat.LastRequest.AuthorUserId);
        Assert.Null(typeof(ChatServiceRequest).GetProperty("ApiVersion"));
        Assert.Equal(new[] { "reply" }, reply.LastMessages);
    }
}
