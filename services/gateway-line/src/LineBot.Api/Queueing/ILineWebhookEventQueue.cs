// SPDX-License-Identifier: MIT
namespace LineBot.Api.Queueing;

public enum QueueWriteResult
{
    Accepted,
    Full
}

public interface ILineWebhookEventQueue
{
    Task<QueueWriteResult> TryEnqueueAsync(QueueItem item, CancellationToken cancellationToken = default);
    Task RunAsync(Func<QueueItem, CancellationToken, Task> processItem, CancellationToken cancellationToken = default);
}
