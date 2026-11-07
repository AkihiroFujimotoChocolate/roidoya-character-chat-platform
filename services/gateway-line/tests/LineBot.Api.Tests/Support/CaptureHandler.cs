// SPDX-License-Identifier: MIT
using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;

namespace LineBot.Api.Tests.Support;

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
    internal CaptureHandler(Func<CapturedHttp, CancellationToken, Task<HttpResponseMessage>> respond) =>
        _respond = respond;
    internal async Task<CapturedHttp> NextAsync()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        return await _seen.Reader.ReadAsync(deadline.Token);
    }
    internal async Task AssertNoPendingRequestsAsync()
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
