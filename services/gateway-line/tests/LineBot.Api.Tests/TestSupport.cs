// SPDX-License-Identifier: MIT
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace LineBot.Api.Tests;

internal static class TestSupport
{
    public static IConfiguration BuildConfiguration(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    public static NullLogger<T> Logger<T>() => NullLogger<T>.Instance;
}

internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

    public StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
    {
        _handler = handler;
    }

    public HttpRequestMessage? LastRequest { get; private set; }
    public CancellationToken LastCancellationToken { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        LastCancellationToken = cancellationToken;
        return await _handler(request, cancellationToken);
    }
}

internal sealed class CapturingChatService : LineBot.Api.Services.IChatService
{
    private readonly TaskCompletionSource<bool> _invoked = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public LineBot.Api.Models.ChatServiceRequest? LastRequest { get; private set; }
    public CancellationToken LastCancellationToken { get; private set; }

    public Task<LineBot.Api.Models.ChatServiceResult> GenerateReplyAsync(LineBot.Api.Models.ChatServiceRequest request, CancellationToken cancellationToken = default)
    {
        LastRequest = request;
        LastCancellationToken = cancellationToken;
        _invoked.TrySetResult(true);
        return Task.FromResult(new LineBot.Api.Models.ChatServiceResult
        {
            Status = "ok",
            Messages = new List<string> { "reply" }
        });
    }

    public Task WaitForInvocationAsync(TimeSpan timeout)
    {
        return _invoked.Task.WaitAsync(timeout);
    }
}

internal sealed class StubQueueService : LineBot.Api.Services.ILocalQueueService
{
    private readonly Queue<LineBot.Api.Models.QueueItem> _queue = new();

    public void Enqueue(LineBot.Api.Models.QueueItem item) => _queue.Enqueue(item);

    public bool TryEnqueue(LineBot.Api.Models.QueueItem item)
    {
        _queue.Enqueue(item);
        return true;
    }

    public LineBot.Api.Models.QueueItem? TryDequeue() => _queue.Count > 0 ? _queue.Dequeue() : null;

    public int Count => _queue.Count;

    public bool IsFull => false;
}

internal sealed class StubUserLockService : LineBot.Api.Services.IUserLockService
{
    public bool TryAcquireLock(string userKey) => true;
    public void ReleaseLock(string userKey) { }
    public bool IsLocked(string userKey) => false;
}

internal sealed class StubIdempotencyService : LineBot.Api.Services.IIdempotencyService
{
    public bool IsProcessed(string webhookEventId) => false;
    public void MarkAsProcessed(string webhookEventId) { }
}

internal sealed class CapturingLineReplyService : LineBot.Api.Services.ILineReplyService
{
    private readonly TaskCompletionSource<bool> _invoked = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public string? LastReplyToken { get; private set; }
    public List<string>? LastMessages { get; private set; }
    public string? LastWebhookEventId { get; private set; }
    public bool? LastIsRedelivery { get; private set; }
    public CancellationToken LastCancellationToken { get; private set; }

    public Task<bool> SendReplyAsync(string replyToken, List<string> messages, string webhookEventId, bool isRedelivery, CancellationToken cancellationToken = default)
    {
        LastReplyToken = replyToken;
        LastMessages = messages;
        LastWebhookEventId = webhookEventId;
        LastIsRedelivery = isRedelivery;
        LastCancellationToken = cancellationToken;
        _invoked.TrySetResult(true);
        return Task.FromResult(true);
    }

    public Task WaitForInvocationAsync(TimeSpan timeout)
    {
        return _invoked.Task.WaitAsync(timeout);
    }
}
