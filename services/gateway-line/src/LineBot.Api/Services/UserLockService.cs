// SPDX-License-Identifier: MIT
using System.Collections.Concurrent;

namespace LineBot.Api.Services;

public interface IUserLockService
{
    bool TryAcquireLock(string userKey);
    void ReleaseLock(string userKey);
    bool IsLocked(string userKey);
}

public class UserLockService : IUserLockService
{
    private readonly ConcurrentDictionary<string, DateTime> _userLocks = new();
    private readonly bool _enabled;
    private readonly TimeSpan _lockTimeout;
    private readonly ILogger<UserLockService> _logger;

    public UserLockService(IConfiguration configuration, ILogger<UserLockService> logger)
    {
        _enabled = configuration.GetValue<bool>("Local:Locks:Enabled", true);
        _lockTimeout = TimeSpan.FromSeconds(configuration.GetValue<int>("Lock:UserTimeoutSeconds", 300));
        _logger = logger;

        // Cleanup expired locks periodically
        _ = Task.Run(CleanupExpiredLocks);
    }

    public bool TryAcquireLock(string userKey)
    {
        if (!_enabled)
        {
            return true; // Locking disabled, always allow
        }

        CleanupExpiredLockForUser(userKey);

        var lockExpiry = DateTime.UtcNow.Add(_lockTimeout);
        return _userLocks.TryAdd(userKey, lockExpiry);
    }

    public void ReleaseLock(string userKey)
    {
        if (!_enabled) return;

        _userLocks.TryRemove(userKey, out _);
        _logger.LogDebug("Released lock for user {UserKey}", userKey);
    }

    public bool IsLocked(string userKey)
    {
        if (!_enabled) return false;

        CleanupExpiredLockForUser(userKey);
        return _userLocks.ContainsKey(userKey);
    }

    private void CleanupExpiredLockForUser(string userKey)
    {
        if (_userLocks.TryGetValue(userKey, out var expiry) && expiry <= DateTime.UtcNow)
        {
            _userLocks.TryRemove(userKey, out _);
            _logger.LogDebug("Cleaned up expired lock for user {UserKey}", userKey);
        }
    }

    private async Task CleanupExpiredLocks()
    {
        while (true)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(1)); // Cleanup every minute

                var now = DateTime.UtcNow;
                var expiredKeys = _userLocks
                    .Where(kvp => kvp.Value <= now)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in expiredKeys)
                {
                    _userLocks.TryRemove(key, out _);
                }

                if (expiredKeys.Count > 0)
                {
                    _logger.LogDebug("Cleaned up {Count} expired locks", expiredKeys.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during lock cleanup");
            }
        }
    }
}