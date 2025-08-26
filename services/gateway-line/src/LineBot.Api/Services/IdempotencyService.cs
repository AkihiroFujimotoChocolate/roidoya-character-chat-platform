// SPDX-License-Identifier: MIT
using System.Collections.Concurrent;

namespace LineBot.Api.Services;

public interface IIdempotencyService
{
    bool IsProcessed(string webhookEventId);
    void MarkAsProcessed(string webhookEventId);
}

public class IdempotencyService : IIdempotencyService
{
    private readonly ConcurrentDictionary<string, DateTime> _processedEvents = new();
    private readonly TimeSpan _ttl = TimeSpan.FromHours(1); // Keep for 1 hour
    private readonly ILogger<IdempotencyService> _logger;

    public IdempotencyService(ILogger<IdempotencyService> logger)
    {
        _logger = logger;
        
        // Cleanup expired entries periodically
        _ = Task.Run(CleanupExpiredEntries);
    }

    public bool IsProcessed(string webhookEventId)
    {
        CleanupExpiredEntry(webhookEventId);
        return _processedEvents.ContainsKey(webhookEventId);
    }

    public void MarkAsProcessed(string webhookEventId)
    {
        var expiry = DateTime.UtcNow.Add(_ttl);
        _processedEvents.TryAdd(webhookEventId, expiry);
        _logger.LogDebug("Marked webhook event {WebhookEventId} as processed", webhookEventId);
    }

    private void CleanupExpiredEntry(string webhookEventId)
    {
        if (_processedEvents.TryGetValue(webhookEventId, out var expiry) && expiry <= DateTime.UtcNow)
        {
            _processedEvents.TryRemove(webhookEventId, out _);
        }
    }

    private async Task CleanupExpiredEntries()
    {
        while (true)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(5)); // Cleanup every 5 minutes

                var now = DateTime.UtcNow;
                var expiredKeys = _processedEvents
                    .Where(kvp => kvp.Value <= now)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in expiredKeys)
                {
                    _processedEvents.TryRemove(key, out _);
                }

                if (expiredKeys.Count > 0)
                {
                    _logger.LogDebug("Cleaned up {Count} expired idempotency entries", expiredKeys.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during idempotency cleanup");
            }
        }
    }
}