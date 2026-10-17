// SPDX-License-Identifier: MIT
using LineBot.Api.Services;
using LineBot.Api.Tests.Support;
using Microsoft.Extensions.Logging;
namespace LineBot.Api.Tests.ExternalContracts.V01;

public sealed class ChatHttpRequestContractTests
{
    // External-01: Verifies all v0.1 request fields and supplied or generated request IDs.
    [Theory]
    [InlineData("correlation-test")] // External-01-01: requestId supplied
    [InlineData(null)]               // External-01-02: requestId omitted
    [Trait("Case", "External-01")]
    public async Task Request_fields_preserve_their_meaning(string? requestId)
    {
        using var handler = new CaptureHandler();
        using var client = handler.Client();
        var request = Samples.ChatInput(); request.RequestId = requestId;
        var service = new HttpV01ChatService(client, Samples.Config(), new LogBook().For<HttpV01ChatService>());
        await service.GenerateReplyAsync(request);
        var sent = Assert.Single(handler.Requests);
        V01RequestAssertions.Body(sent, requestId);
        V01RequestAssertions.Headers(sent);
        Assert.Equal(new Uri("https://chat.test/chat/v0.1/generate-replies"), sent.Uri);
    }

    // External-02: Verifies version-header naming and Chat authentication without changing the request body or route.
    [Theory]
    [InlineData(null, null)]                        // External-02-01: default version header; no Chat API key
    [InlineData(null, "key-test")]                  // External-02-02: default version header; Chat API key supplied
    [InlineData("X-Test-Chat-Version", null)]       // External-02-03: custom version header; no Chat API key
    [InlineData("X-Test-Chat-Version", "key-test")] // External-02-04: custom version header; Chat API key supplied
    [Trait("Case", "External-02")]
    public async Task Headers_and_chat_authentication(string? headerName, string? key)
    {
        using var handler = new CaptureHandler(); using var client = handler.Client();
        var settings = new List<(string, string?)> { ("Chat:Http:ApiKey", key) };
        if (headerName is not null) settings.Add(("Chat:Http:VersionHeaderName", headerName));
        var service = new HttpV01ChatService(client, Samples.Config(settings.ToArray()), new LogBook().For<HttpV01ChatService>());
        await service.GenerateReplyAsync(Samples.ChatInput());
        var sent = Assert.Single(handler.Requests);
        V01RequestAssertions.Headers(sent, headerName, key);
        V01RequestAssertions.Body(sent);
        Assert.Equal(new Uri("https://chat.test/chat/v0.1/generate-replies"), sent.Uri);
    }

    // External-03 / External-03-01: Verifies valid additional headers while preserving the v0.1 request contract.
    [Fact, Trait("Case", "External-03")]
    public async Task Additional_headers_keep_the_normal_body_and_content_type()
    {
        using var handler = new CaptureHandler(); using var client = handler.Client();
        var service = new HttpV01ChatService(client, Samples.Config(
            ("Chat:Http:AdditionalHeaders", "{\"X-Test-A\":\"a\",\"X-Test-B\":\"b\"}")), new LogBook().For<HttpV01ChatService>());
        await service.GenerateReplyAsync(Samples.ChatInput());
        var sent = Assert.Single(handler.Requests);
        Assert.Equal("a", sent.Header("X-Test-A")); Assert.Equal("b", sent.Header("X-Test-B"));
        V01RequestAssertions.Body(sent);
        V01RequestAssertions.Headers(sent);
        Assert.Equal(new Uri("https://chat.test/chat/v0.1/generate-replies"), sent.Uri);
    }

    // External-04: Verifies invalid additional headers are omitted without corrupting the normal request.
    [Theory]
    [InlineData("{\"Content-Type\":\"text/plain\"}", "Content-Type")]   // External-04-01: Content-Type cannot be added as a request header
    [InlineData("{\"X-Test-Bad\":\"line1\\r\\nline2\"}", "X-Test-Bad")] // External-04-02: header value contains CR/LF
    [Trait("Case", "External-04")]
    public async Task Invalid_additional_header_is_not_sent_or_moved_to_content(string setting, string header)
    {
        using var handler = new CaptureHandler(); using var client = handler.Client(); var logs = new LogBook();
        var service = new HttpV01ChatService(client, Samples.Config(("Chat:Http:AdditionalHeaders", setting)), logs.For<HttpV01ChatService>());
        await service.GenerateReplyAsync(Samples.ChatInput());
        var sent = Assert.Single(handler.Requests);
        Assert.False(sent.Headers.ContainsKey(header));
        V01RequestAssertions.Body(sent);
        V01RequestAssertions.Headers(sent);
        Assert.Equal(new Uri("https://chat.test/chat/v0.1/generate-replies"), sent.Uri);
        if (header != "Content-Type") Assert.False(sent.ContentHeaders.ContainsKey(header));
        Assert.Contains(logs.Entries, e => e.Level == LogLevel.Warning);
    }

    // External-14 / External-14-01: Verifies Unicode, escapes and whitespace survive JSON conversion without changing other request fields.
    [Fact, Trait("Case", "External-14")]
    public async Task Text_round_trip_retains_unicode_escapes_and_whitespace()
    {
        using var handler = new CaptureHandler(); using var client = handler.Client();
        var service = new HttpV01ChatService(client, Samples.Config(), new LogBook().For<HttpV01ChatService>());
        await service.GenerateReplyAsync(Samples.ChatInput(Samples.RichText));
        var sent = Assert.Single(handler.Requests);
        V01RequestAssertions.Body(sent, text: Samples.RichText);
        V01RequestAssertions.Headers(sent);
        Assert.Equal(new Uri("https://chat.test/chat/v0.1/generate-replies"), sent.Uri);
    }
}
