// SPDX-License-Identifier: MIT
using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using LineBot.Api.Models;
using LineBot.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LineBot.Api.Tests.Support;

internal static class Samples
{
    internal const string Secret = "synthetic-channel-secret";
    internal const string AccessToken = "access-token-test";
    internal const string RichText = "  こんにちは😀\n\"引用\"\\path  ";
    internal const string DefaultFallback = "The service is temporarily unavailable.";
    internal const string TimeoutFallback = "The request timed out. Please try again.";
    internal static IConfiguration Config(params (string Key, string? Value)[] settings)
    {
        var values = new Dictionary<string, string?> { ["Chat:Http:BaseUrl"] = "https://chat.test/",
            ["Chat:ApiVersion"] = "0.1", ["Line:ChannelAccessToken"] = AccessToken,
            ["Chat:Fallbacks:Default"] = "default-test", ["Reply:Qps"] = "100" };
        foreach (var (key, value) in settings) values[key] = value;
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
    // Fixtures are made by the test, never by the production serializer or mapper.
    internal static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    internal static HttpResponseMessage Ok(params string[] messages) =>
        Json(JsonSerializer.Serialize(new { request_id = "correlation-test", status = "ok", messages, fallback_used = false }));
    internal static byte[] Webhook(bool redelivery = false, string mode = "active", string kind = "message",
        string messageKind = "text", string text = "こんにちは", string eventId = "event-test", string person = "person-test",
        string replyToken = "reply-test", string messageId = "message-test", string destination = "destination-test")
    {
        var evt = new Dictionary<string, object?> {
            ["type"] = kind, ["mode"] = mode, ["timestamp"] = 1700000000000L, ["webhookEventId"] = eventId,
            ["source"] = new { type = "user", userId = person }, ["replyToken"] = replyToken,
            ["deliveryContext"] = new { isRedelivery = redelivery }
        };
        if (kind == "message") {
            evt["message"] = messageKind == "text"
                ? (object)new { id = messageId, type = "text", text }
                : new { id = messageId, type = "image", contentProvider = new { type = "line" } };
        }
        return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { destination, events = new[] { evt } }));
    }
    internal static string Sign(byte[] bytes, string secret = Secret) =>
        Convert.ToBase64String(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), bytes));
    internal static HttpRequestMessage Incoming(byte[] bytes, string? signature) {
        var request = new HttpRequestMessage(HttpMethod.Post, "/line/webhook")
        { Content = new ByteArrayContent(bytes) };
        request.Content.Headers.ContentType = new("application/json") { CharSet = "utf-8" };
        if (signature is not null) request.Headers.Add("X-Line-Signature", signature);
        return request;
    }
}

internal sealed record CapturedHttp(HttpMethod Method, Uri Uri, byte[] Body,
    IReadOnlyDictionary<string, string[]> Headers, IReadOnlyDictionary<string, string[]> ContentHeaders)
{
    internal JsonDocument Json() => JsonDocument.Parse(Body);
    internal string Header(string name) => string.Join(",", Headers[name]);
}
internal sealed class CaptureHandler : HttpMessageHandler
{
    private readonly Func<CapturedHttp, CancellationToken, Task<HttpResponseMessage>> _respond;
    private readonly Channel<CapturedHttp> _seen = Channel.CreateUnbounded<CapturedHttp>();
    internal ConcurrentQueue<CapturedHttp> Requests { get; } = new();
    internal CaptureHandler(Func<CapturedHttp, CancellationToken, Task<HttpResponseMessage>>? respond = null) =>
        _respond = respond ?? ((_, _) => Task.FromResult(Samples.Ok("reply-a")));
    internal async Task<CapturedHttp> NextAsync()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        return await _seen.Reader.ReadAsync(deadline.Token);
    }
    internal async Task ExpectQuietAsync()
    {
        using var window = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));
        try {
            var readable = await _seen.Reader.WaitToReadAsync(window.Token);
            Assert.False(readable, "Unexpected additional outbound HTTP request.");
        }
        catch (OperationCanceledException) when (window.IsCancellationRequested) { }
    }
    internal HttpClient Client() => new(this, disposeHandler: false) { Timeout = Timeout.InfiniteTimeSpan };
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var captured = new CapturedHttp(request.Method, request.RequestUri!,
            request.Content is null ? Array.Empty<byte>() : await request.Content.ReadAsByteArrayAsync(cancellationToken),
            request.Headers.ToDictionary(h => h.Key, h => h.Value.ToArray(), StringComparer.OrdinalIgnoreCase),
            request.Content?.Headers.ToDictionary(h => h.Key, h => h.Value.ToArray(), StringComparer.OrdinalIgnoreCase)
                ?? new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase));
        Requests.Enqueue(captured);
        await _seen.Writer.WriteAsync(captured, cancellationToken);
        return await _respond(captured, cancellationToken);
    }
}

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

// Bounded, per-test queue. Its second dequeue is a deterministic observation
// that the preceding job has finished; it does not test queue capacity or races.
internal sealed class ObservedQueue : ILocalQueueService
{
    private readonly ConcurrentQueue<QueueItem> _items = new();
    private int _accepted;
    private int _taken;
    internal TaskCompletionSource<QueueItem> Accepted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource AfterJob { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public bool TryEnqueue(QueueItem item) { _items.Enqueue(item); Interlocked.Increment(ref _accepted); Accepted.TrySetResult(item); return true; }
    public QueueItem? TryDequeue() {
        if (_items.TryDequeue(out var item)) { Interlocked.Increment(ref _taken); return item; }
        if (Volatile.Read(ref _taken) > 0) AfterJob.TrySetResult();
        return null;
    }
    public int Count => _items.Count;
    public bool IsFull => false;
    internal int AcceptedCount => Volatile.Read(ref _accepted);
}
internal sealed class TestLock : IUserLockService
{
    public bool TryAcquireLock(string key) => true;
    public void ReleaseLock(string key) { }
    public bool IsLocked(string key) => false;
}
internal sealed class TestIdempotency : IIdempotencyService
{
    public bool IsProcessed(string eventId) => false;
    public void MarkAsProcessed(string eventId) { }
}
internal sealed class ChatDouble : IChatService
{
    internal ConcurrentQueue<(ChatServiceRequest Request, CancellationToken Token)> Calls { get; } = new();
    internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal Func<ChatServiceRequest, CancellationToken, Task<ChatServiceResult>> Respond { get; set; } =
        (_, _) => Task.FromResult(new ChatServiceResult { Status = "ok", Messages = new() { "selected-a", "selected-b" } });
    public Task<ChatServiceResult> GenerateReplyAsync(ChatServiceRequest request, CancellationToken cancellationToken = default) {
        Calls.Enqueue((request, cancellationToken)); Started.TrySetResult(); return Respond(request, cancellationToken);
    }
}
internal sealed record ReplyCall(string Token, List<string> Messages, string EventId, bool Redelivery, CancellationToken Cancellation);
internal sealed class ReplyDouble : ILineReplyService
{
    internal ConcurrentQueue<ReplyCall> Calls { get; } = new();
    internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal Func<CancellationToken, Task<bool>> Respond { get; set; } = _ => Task.FromResult(true);
    public Task<bool> SendReplyAsync(string replyToken, List<string> messages, string webhookEventId, bool isRedelivery,
        CancellationToken cancellationToken = default) {
        Calls.Enqueue(new(replyToken, messages.ToList(), webhookEventId, isRedelivery, cancellationToken));
        Started.TrySetResult(); return Respond(cancellationToken);
    }
}
internal sealed class WorkerRig : IAsyncDisposable
{
    internal ObservedQueue Queue { get; } = new();
    internal ChatDouble Chat { get; } = new();
    internal ReplyDouble Reply { get; } = new();
    internal LogBook Logs { get; } = new();
    private readonly ServiceProvider _services;
    internal MessageWorkerService Worker { get; }
    internal IConfiguration Settings { get; } = Samples.Config();
    internal WorkerRig() {
        var services = new ServiceCollection();
        services.AddSingleton<ILocalQueueService>(Queue);
        services.AddSingleton<IUserLockService>(new TestLock());
        services.AddSingleton<IIdempotencyService>(new TestIdempotency());
        services.AddSingleton<IChatService>(Chat);
        services.AddSingleton<ILineReplyService>(Reply);
        _services = services.BuildServiceProvider();
        Worker = new(_services, Logs.For<MessageWorkerService>(), Settings);
    }
    internal async Task RunAsync(QueueItem item) {
        Queue.TryEnqueue(item);
        await Worker.StartAsync(CancellationToken.None);
        await Queue.AfterJob.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }
    internal async Task StopAsync() {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await Worker.StopAsync(deadline.Token);
    }
    public async ValueTask DisposeAsync() {
        await StopAsync(); Worker.Dispose(); await _services.DisposeAsync();
    }
}

// The real Program registers the mode switch and hosted Worker. Only transport,
// ancillary stores and (when requested) the abstract services are replaced.
internal sealed class GatewayFactory : WebApplicationFactory<global::Program>
{
    internal CaptureHandler ChatHttp { get; }
    internal CaptureHandler LineHttp { get; }
    internal LogBook Logs { get; } = new();
    internal ObservedQueue Queue { get; } = new();
    internal ChatDouble? ChatOverride { get; init; }
    internal ReplyDouble? ReplyOverride { get; init; }
    internal Dictionary<string, string?> Settings { get; } = new() {
        ["Chat:Mode"] = "Http", ["Chat:ApiVersion"] = "0.1",
        ["Chat:Http:BaseUrl"] = "https://chat.test/", ["Chat:RequestTimeoutSeconds"] = "1",
        ["Line:ChannelSecret"] = Samples.Secret, ["Line:ChannelAccessToken"] = Samples.AccessToken,
        ["Reply:Qps"] = "100", ["Chat:Fallbacks:Default"] = Samples.DefaultFallback,
        ["Chat:Fallbacks:Timeout"] = Samples.TimeoutFallback
    };
    internal GatewayFactory(CaptureHandler? chat = null, CaptureHandler? line = null) {
        ChatHttp = chat ?? new();
        LineHttp = line ?? new((_, _) => Task.FromResult(Samples.Json("{\"sentMessages\":[{\"id\":\"sent-test\",\"quoteToken\":\"quote-test\"}]}")));
    }
    protected override void ConfigureWebHost(IWebHostBuilder builder) {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => {
            config.Sources.Clear(); // Do not inherit workstation credentials or routing.
            config.AddInMemoryCollection(Settings);
        });
        builder.ConfigureTestServices(services => {
            services.AddLogging(logging => logging.AddProvider(Logs));
            services.RemoveAll<ILocalQueueService>(); services.AddSingleton<ILocalQueueService>(Queue);
            services.RemoveAll<IUserLockService>(); services.AddSingleton<IUserLockService>(new TestLock());
            services.RemoveAll<IIdempotencyService>(); services.AddSingleton<IIdempotencyService>(new TestIdempotency());
            // The existing scoped HttpV01ChatService constructor accepts HttpClient.
            services.AddSingleton<HttpClient>(_ => ChatHttp.Client());
            services.AddHttpClient<ILineReplyService, LineReplyService>().ConfigurePrimaryHttpMessageHandler(() => LineHttp);
            if (ChatOverride is not null) { services.RemoveAll<IChatService>(); services.AddSingleton<IChatService>(ChatOverride); }
            if (ReplyOverride is not null) { services.RemoveAll<ILineReplyService>(); services.AddSingleton<ILineReplyService>(ReplyOverride); }
        });
    }
    internal async Task StopWorkerAsync() {
        var workers = Services.GetServices<IHostedService>().OfType<MessageWorkerService>().ToArray();
        foreach (var worker in workers) {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await worker.StopAsync(deadline.Token);
        }
    }
}
