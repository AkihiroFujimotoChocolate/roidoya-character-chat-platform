// SPDX-License-Identifier: MIT
using LineBot.Api.Services;
using LineBot.Api.Tests.Support;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;
namespace LineBot.Api.Tests.ExternalContracts.V01;

public sealed class ChatHttpRequestContractTests : DiagnosticTest
{
    public ChatHttpRequestContractTests(ITestOutputHelper output) : base(output) { }

    // External-01: Verifies all v0.1 request fields and supplied or generated request IDs.
    [Theory]
    [InlineData(true)]  // External-01-01: requestId supplied
    [InlineData(false)] // External-01-02: requestId omitted
    [Trait("Case", "External-01")]
    public async Task Request_fields_preserve_their_meaning(bool supplied)
    {
        using var handler = new CaptureHandler();
        using var client = handler.Client();
        var sample = supplied ? RequestSamples.CreateRandom() : RequestSamples.CreateWithoutRequestId();
        var settings = Samples.Config();
        Trace(supplied ? "External-01-01" : "External-01-02", new { sample.Values, sample.Expected }, settings);
        var service = new HttpV01ChatService(client, settings, new LogBook().For<HttpV01ChatService>());
        await service.GenerateReplyAsync(sample.Input);
        var sent = Assert.Single(handler.Requests);
        V01RequestAssertions.Body(sent, sample.Expected);
        V01RequestAssertions.Headers(sent);
        Assert.Equal(new Uri("https://chat.test/chat/v0.1/generate-replies"), sent.Uri);
    }

    // External-02: Verifies version-header naming and Chat authentication without changing the request body or route.
    [Theory]
    [InlineData(null, false)]                  // External-02-01: default version header; no Chat API key
    [InlineData(null, true)]                   // External-02-02: default version header; Chat API key supplied
    [InlineData("X-Test-Chat-Version", false)] // External-02-03: custom version header; no Chat API key
    [InlineData("X-Test-Chat-Version", true)]  // External-02-04: custom version header; Chat API key supplied
    [Trait("Case", "External-02")]
    public async Task Headers_and_chat_authentication(string? headerName, bool suppliedKey)
    {
        using var handler = new CaptureHandler(); using var client = handler.Client();
        var sample = RequestSamples.CreateRandom();
        var key = suppliedKey ? GeneratedValues.Id("chat-key") : null;
        var settings = new List<(string, string?)> { ("Chat:Http:ApiKey", key) };
        if (headerName is not null) settings.Add(("Chat:Http:VersionHeaderName", headerName));
        var configuration = Samples.Config(settings.ToArray());
        var condition = (headerName is null ? 1 : 3) + (suppliedKey ? 1 : 0);
        Trace($"External-02-{condition:00}", new { sample.Values, sample.Expected }, configuration);
        var service = new HttpV01ChatService(client, configuration, new LogBook().For<HttpV01ChatService>());
        await service.GenerateReplyAsync(sample.Input);
        var sent = Assert.Single(handler.Requests);
        V01RequestAssertions.Headers(sent, headerName, key);
        V01RequestAssertions.Body(sent, sample.Expected);
        Assert.Equal(new Uri("https://chat.test/chat/v0.1/generate-replies"), sent.Uri);
    }

    // External-03 / External-03-01: Verifies valid additional headers while preserving the v0.1 request contract.
    [Fact, Trait("Case", "External-03")]
    public async Task Additional_headers_keep_the_normal_body_and_content_type()
    {
        using var handler = new CaptureHandler(); using var client = handler.Client();
        var sample = RequestSamples.CreateRandom();
        var first = GeneratedValues.Id("header-a"); var second = GeneratedValues.Id("header-b");
        var additional = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, string> {
            ["X-Test-A"] = first, ["X-Test-B"] = second
        });
        var settings = Samples.Config(("Chat:Http:AdditionalHeaders", additional));
        Trace("External-03-01", new { sample.Values, sample.Expected }, settings);
        var service = new HttpV01ChatService(client, settings, new LogBook().For<HttpV01ChatService>());
        await service.GenerateReplyAsync(sample.Input);
        var sent = Assert.Single(handler.Requests);
        Assert.Equal(first, sent.Header("X-Test-A")); Assert.Equal(second, sent.Header("X-Test-B"));
        V01RequestAssertions.Body(sent, sample.Expected);
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
        var sample = RequestSamples.CreateRandom();
        var settings = Samples.Config(("Chat:Http:AdditionalHeaders", setting));
        Trace(header == "Content-Type" ? "External-04-01" : "External-04-02", new { sample.Values, sample.Expected, header }, settings);
        var service = new HttpV01ChatService(client, settings, logs.For<HttpV01ChatService>());
        await service.GenerateReplyAsync(sample.Input);
        var sent = Assert.Single(handler.Requests);
        Assert.False(sent.Headers.ContainsKey(header));
        V01RequestAssertions.Body(sent, sample.Expected);
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
        var sample = RequestSamples.CreateRandom(text: Samples.RichText);
        var settings = Samples.Config();
        Trace("External-14-01", new { sample.Values, sample.Expected }, settings);
        var service = new HttpV01ChatService(client, settings, new LogBook().For<HttpV01ChatService>());
        await service.GenerateReplyAsync(sample.Input);
        var sent = Assert.Single(handler.Requests);
        V01RequestAssertions.Body(sent, sample.Expected);
        V01RequestAssertions.Headers(sent);
        Assert.Equal(new Uri("https://chat.test/chat/v0.1/generate-replies"), sent.Uri);
    }
}
