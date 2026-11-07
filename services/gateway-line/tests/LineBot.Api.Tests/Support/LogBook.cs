// SPDX-License-Identifier: MIT
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace LineBot.Api.Tests.Support;

internal sealed record LogEntry(string Category, LogLevel Level, string Message,
    IReadOnlyDictionary<string, object?> Values, Exception? Exception);
internal sealed class LogBook : ILoggerProvider
{
    private readonly SemaphoreSlim _changed = new(0);
    internal ConcurrentQueue<LogEntry> Entries { get; } = new();
    public ILogger CreateLogger(string categoryName) => new Recorder(this, categoryName);
    internal ILogger<T> For<T>() => new TypedRecorder<T>(this);
    internal async Task<LogEntry> WaitAsync(Func<LogEntry, bool> predicate)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (true) {
            var entry = Entries.FirstOrDefault(predicate);
            if (entry is not null) return entry;
            await _changed.WaitAsync(deadline.Token);
        }
    }
    private void Append<T>(string category, LogLevel level, T state, Exception? exception, Func<T, Exception?, string> format)
    {
        var values = state is IEnumerable<KeyValuePair<string, object?>> pairs
            ? pairs.ToDictionary(p => p.Key, p => p.Value) : new Dictionary<string, object?>();
        Entries.Enqueue(new(category, level, format(state, exception), values, exception));
        _changed.Release();
    }
    public void Dispose() { }
    private sealed class Recorder(LogBook owner, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => owner.Append(category, level, state, exception, formatter);
    }
    private sealed class TypedRecorder<T>(LogBook owner) : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => owner.Append(typeof(T).FullName!, level, state, exception, formatter);
    }
}
