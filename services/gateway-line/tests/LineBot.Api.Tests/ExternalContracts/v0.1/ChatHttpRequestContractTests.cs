// SPDX-License-Identifier: MIT
using System.Text.Json;
using LineBot.Api.Services;
using LineBot.Api.Tests.Support;
using Microsoft.Extensions.Logging;
namespace LineBot.Api.Tests.ExternalContracts.V01;

public sealed class ChatHttpRequestContractTests
{
    // External-01: fixed request contract, including the absence of extra wire fields.
    [Theory]
    [InlineData("correlation-test")]
    [InlineData(null)]
    [Trait("Case", "External-01")]
    public async Task Request_fields_preserve_their_meaning(string? requestId)
    {
        using var handler = new CaptureHandler();
        using var client = handler.Client();
        var request = Samples.ChatInput(); request.RequestId = requestId;
        var service = new HttpV01ChatService(client, Samples.Config(), new LogBook().For<HttpV01ChatService>());
        await service.GenerateReplyAsync(request);
        var sent = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, sent.Method);
        using var json = sent.Json(); var body = json.RootElement;
        Assert.Equal(new[] { "author", "conversation", "event_id", "limits", "message", "origin", "request_id" },
            body.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray());
        Assert.Equal(JsonValueKind.String, body.GetProperty("request_id").ValueKind);
        if (requestId is null) Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("request_id").GetString()));
        else Assert.Equal(requestId, body.GetProperty("request_id").GetString());
        Assert.Equal("", body.GetProperty("event_id").GetString());
        Assert.Equal("line", body.GetProperty("origin").GetProperty("platform").GetString());
        Assert.Equal("conversation-test", body.GetProperty("conversation").GetProperty("id").GetString());
        Assert.Equal("line-author-test", body.GetProperty("author").GetProperty("user_id").GetString());
        Assert.Equal("こんにちは", body.GetProperty("message").GetProperty("text").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("message").GetProperty("language").ValueKind);
        Assert.Equal(7, body.GetProperty("limits").GetProperty("timeout_seconds").GetInt32());
        Assert.Equal(123, body.GetProperty("limits").GetProperty("max_chars_per_message").GetInt32());
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(null, "key-test")]
    [InlineData("X-Test-Chat-Version", null)]
    [InlineData("X-Test-Chat-Version", "key-test")]
    [Trait("Case", "External-02")]
    public async Task Headers_and_chat_authentication(string? headerName, string? key)
    {
        using var handler = new CaptureHandler(); using var client = handler.Client();
        var settings = new List<(string, string?)> { ("Chat:Http:ApiKey", key) };
        if (headerName is not null) settings.Add(("Chat:Http:VersionHeaderName", headerName));
        var service = new HttpV01ChatService(client, Samples.Config(settings.ToArray()), new LogBook().For<HttpV01ChatService>());
        await service.GenerateReplyAsync(Samples.ChatInput());
        var sent = Assert.Single(handler.Requests);
        Assert.Equal("0.1", sent.Header(headerName ?? "X-Chat-Api-Version"));
        Assert.Equal("application/json; charset=utf-8", Assert.Single(sent.ContentHeaders["Content-Type"]));
        if (key is null) Assert.False(sent.Headers.ContainsKey("Authorization"));
        else Assert.Equal("Bearer key-test", sent.Header("Authorization"));
    }

    [Fact, Trait("Case", "External-03")]
    public async Task Additional_headers_keep_the_normal_body_and_content_type()
    {
        using var handler = new CaptureHandler(); using var client = handler.Client();
        var service = new HttpV01ChatService(client, Samples.Config(
            ("Chat:Http:AdditionalHeaders", "{\"X-Test-A\":\"a\",\"X-Test-B\":\"b\"}")), new LogBook().For<HttpV01ChatService>());
        await service.GenerateReplyAsync(Samples.ChatInput());
        var sent = Assert.Single(handler.Requests);
        Assert.Equal("a", sent.Header("X-Test-A")); Assert.Equal("b", sent.Header("X-Test-B"));
        Assert.Equal("application/json; charset=utf-8", Assert.Single(sent.ContentHeaders["Content-Type"]));
        using var json = sent.Json();
        Assert.Equal("こんにちは", json.RootElement.GetProperty("message").GetProperty("text").GetString());
        Assert.False(json.RootElement.TryGetProperty("X-Test-A", out _));
    }

    [Theory]
    [InlineData("{\"Content-Type\":\"text/plain\"}", "Content-Type")]
    [InlineData("{\"X-Test-Bad\":\"line1\\r\\nline2\"}", "X-Test-Bad")]
    [Trait("Case", "External-04")]
    public async Task Invalid_additional_header_is_not_sent_or_moved_to_content(string setting, string header)
    {
        using var handler = new CaptureHandler(); using var client = handler.Client(); var logs = new LogBook();
        var service = new HttpV01ChatService(client, Samples.Config(("Chat:Http:AdditionalHeaders", setting)), logs.For<HttpV01ChatService>());
        await service.GenerateReplyAsync(Samples.ChatInput());
        var sent = Assert.Single(handler.Requests);
        Assert.False(sent.Headers.ContainsKey(header));
        Assert.Equal("application/json; charset=utf-8", Assert.Single(sent.ContentHeaders["Content-Type"]));
        if (header != "Content-Type") Assert.False(sent.ContentHeaders.ContainsKey(header));
        Assert.Contains(logs.Entries, e => e.Level == LogLevel.Warning);
    }

    [Fact, Trait("Case", "External-14")]
    public async Task Text_round_trip_retains_unicode_escapes_and_whitespace()
    {
        using var handler = new CaptureHandler(); using var client = handler.Client();
        var service = new HttpV01ChatService(client, Samples.Config(), new LogBook().For<HttpV01ChatService>());
        await service.GenerateReplyAsync(Samples.ChatInput(Samples.RichText));
        using var json = Assert.Single(handler.Requests).Json();
        Assert.Equal(Samples.RichText, json.RootElement.GetProperty("message").GetProperty("text").GetString());
    }
}
