// SPDX-License-Identifier: MIT
using System.Threading.Channels;

namespace LineBot.Api.Queueing.Local;

public sealed class LocalLineWebhookEventQueue : ILineWebhookEventQueue
{
    private readonly Channel<QueueItem> _channel;
    private readonly int _maxWaiting;
    private readonly object _gate = new();
    private int _waiting;

    public LocalLineWebhookEventQueue(IConfiguration configuration)
    {
        var capacity = Math.Max(1, configuration.GetValue<int>("Local:Queue:MaxSize", 15));
        _maxWaiting = Math.Max(1, configuration.GetValue<int>("Reply:QueueMaxSize", 15));
        _channel = Channel.CreateBounded<QueueItem>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
    }

    public Task<QueueWriteResult> TryEnqueueAsync(QueueItem item, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (_waiting >= _maxWaiting || !_channel.Writer.TryWrite(item))
                return Task.FromResult(QueueWriteResult.Full);

            _waiting++;
            return Task.FromResult(QueueWriteResult.Accepted);
        }
    }

    public async Task RunAsync(Func<QueueItem, CancellationToken, Task> processItem, CancellationToken cancellationToken = default)
    {
        await foreach (var item in _channel.Reader.ReadAllAsync(cancellationToken))
        {
            lock (_gate)
                _waiting--;
            await processItem(item, cancellationToken);
        }
    }
}
