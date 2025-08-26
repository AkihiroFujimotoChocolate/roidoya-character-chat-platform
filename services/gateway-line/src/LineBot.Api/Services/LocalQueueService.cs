// SPDX-License-Identifier: MIT
using System.Collections.Concurrent;
using LineBot.Api.Models;

namespace LineBot.Api.Services;

public interface ILocalQueueService
{
    bool TryEnqueue(QueueItem item);
    QueueItem? TryDequeue();
    int Count { get; }
    bool IsFull { get; }
}

public class LocalQueueService : ILocalQueueService
{
    private readonly ConcurrentQueue<QueueItem> _queue = new();
    private readonly int _maxSize;
    private int _currentSize = 0;

    public LocalQueueService(IConfiguration configuration)
    {
        _maxSize = configuration.GetValue<int>("Local:Queue:MaxSize", 15);
    }

    public bool TryEnqueue(QueueItem item)
    {
        if (_currentSize >= _maxSize)
        {
            return false;
        }

        _queue.Enqueue(item);
        Interlocked.Increment(ref _currentSize);
        return true;
    }

    public QueueItem? TryDequeue()
    {
        if (_queue.TryDequeue(out var item))
        {
            Interlocked.Decrement(ref _currentSize);
            return item;
        }
        return null;
    }

    public int Count => _currentSize;
    public bool IsFull => _currentSize >= _maxSize;
}