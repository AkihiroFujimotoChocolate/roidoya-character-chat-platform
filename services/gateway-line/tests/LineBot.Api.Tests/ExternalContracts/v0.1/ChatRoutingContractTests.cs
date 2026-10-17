// SPDX-License-Identifier: MIT
using LineBot.Api.Services;
using LineBot.Api.Tests.Support;
using Microsoft.Extensions.Logging;
namespace LineBot.Api.Tests.ExternalContracts.V01;

public sealed class ChatRoutingContractTests
{
    // External-05 / External-05-01: Verifies an unsupported protocol version is rejected without an HTTP request.
    [Fact, Trait("Case", "External-05")]
    public async Task Explicit_unsupported_version_is_rejected_without_http()
    {
        using var handler = new CaptureHandler(); using var client = handler.Client();
        var rejection = await Record.ExceptionAsync(async () => {
            var service = new HttpV01ChatService(client, Samples.Config(("Chat:ApiVersion", "0.2")), new LogBook().For<HttpV01ChatService>());
            await service.GenerateReplyAsync(Samples.ChatInput());
        });
        Assert.NotNull(rejection); // No requirement on constructor vs invocation, or exception class.
        Assert.Empty(handler.Requests);
    }

    // External-06: Verifies Endpoint takes priority and stays literal while preserving the wire contract.
    [Theory]
    [InlineData("/api/chat/v0.1", "https://chat.test/api/chat/v0.1")]             // External-06-01: explicit endpoint overrides the template
    [InlineData("/literal/{version}", "https://chat.test/literal/%7Bversion%7D")] // External-06-02: literal endpoint keeps the version placeholder
    [Trait("Case", "External-06")]
    public async Task Explicit_endpoint_wins_and_is_not_a_template(string endpoint, string expected)
    {
        var sent = await Send(("Chat:Http:Endpoint", endpoint), ("Chat:Http:EndpointTemplate", "/chat/{version}/generate-replies"));
        Assert.Equal(new Uri(expected), sent.Uri);
        V01RequestAssertions.Body(sent);
        V01RequestAssertions.Headers(sent);
    }

    // External-07 / External-07-01: Verifies template version expansion while preserving the wire contract.
    [Fact, Trait("Case", "External-07")]
    public async Task Template_expands_the_protocol_version()
    {
        var sent = await Send(("Chat:Http:EndpointTemplate", "/chat/{version}/generate-replies"));
        Assert.Equal(new Uri("https://chat.test/chat/v0.1/generate-replies"), sent.Uri);
        V01RequestAssertions.Body(sent);
        V01RequestAssertions.Headers(sent);
    }

    // External-08 / External-08-01: Verifies a template without a placeholder stays literal while preserving the wire contract.
    [Fact, Trait("Case", "External-08")]
    public async Task Template_without_placeholder_is_literal()
    {
        var sent = await Send(("Chat:Http:EndpointTemplate", "/fixed/reply"));
        Assert.Equal(new Uri("https://chat.test/fixed/reply"), sent.Uri);
        V01RequestAssertions.Body(sent);
        V01RequestAssertions.Headers(sent);
    }

    // External-09 / External-09-01: Verifies the default route while preserving the wire contract.
    [Fact, Trait("Case", "External-09")]
    public async Task Missing_overrides_use_the_default_path()
    {
        var sent = await Send();
        Assert.Equal(new Uri("https://chat.test/chat/v0.1/generate-replies"), sent.Uri);
        V01RequestAssertions.Body(sent);
        V01RequestAssertions.Headers(sent);
    }

    // External-10: Verifies an absolute override ignores the base URL and preserves the wire contract.
    [Theory]
    [InlineData("Chat:Http:Endpoint")]         // External-10-01: absolute URL supplied through Endpoint
    [InlineData("Chat:Http:EndpointTemplate")] // External-10-02: absolute URL supplied through EndpointTemplate
    [Trait("Case", "External-10")]
    public async Task Absolute_override_does_not_add_a_base_url(string key)
    {
        var sent = await Send((key, "https://absolute.test/direct"));
        Assert.Equal(new Uri("https://absolute.test/direct"), sent.Uri);
        V01RequestAssertions.Body(sent);
        V01RequestAssertions.Headers(sent);
    }

    // External-11: Verifies Chat/App base URL priority while preserving the wire contract.
    [Theory]
    [InlineData("https://chat.test/", "https://chat.test/reply")] // External-11-01: Chat BaseUrl takes priority over App BaseUrl
    [InlineData(null, "https://app.test/reply")]                  // External-11-02: App BaseUrl is used when Chat BaseUrl is absent
    [Trait("Case", "External-11")]
    public async Task Relative_override_respects_base_url_priority(string? chatBase, string expected)
    {
        var sent = await Send(("Chat:Http:Endpoint", "/reply"),
            ("Chat:Http:BaseUrl", chatBase), ("App:BaseUrl", "https://app.test/"));
        Assert.Equal(new Uri(expected), sent.Uri);
        V01RequestAssertions.Body(sent);
        V01RequestAssertions.Headers(sent);
    }

    // External-12 / External-12-01: Verifies an unresolved relative URL follows the existing failure path without HTTP.
    [Fact, Trait("Case", "External-12")]
    public async Task Unresolvable_relative_url_uses_existing_failure_path()
    {
        using var handler = new CaptureHandler(); using var client = handler.Client(); var logs = new LogBook();
        var service = new HttpV01ChatService(client, Samples.Config(("Chat:Http:Endpoint", "/reply"),
            ("Chat:Http:BaseUrl", null), ("App:BaseUrl", null)), logs.For<HttpV01ChatService>());
        var result = await service.GenerateReplyAsync(Samples.ChatInput());
        Assert.Empty(handler.Requests); Assert.Equal(new[] { "default-test" }, result.Messages);
        Assert.Contains(logs.Entries, e => e.Level == LogLevel.Error);
    }

    // External-13: Verifies route spelling does not change the v0.1 protocol.
    [Theory]
    [InlineData("/unversioned/chat")] // External-13-01: route has no version name
    [InlineData("/chat/v9/reply")]    // External-13-02: route names v9 while the protocol remains v0.1
    [Trait("Case", "External-13")]
    public async Task Route_spelling_does_not_select_a_different_protocol(string endpoint)
    {
        var sent = await Send(("Chat:Http:Endpoint", endpoint));
        Assert.Equal(new Uri("https://chat.test" + endpoint), sent.Uri);
        V01RequestAssertions.Body(sent);
        V01RequestAssertions.Headers(sent);
    }
    private static async Task<CapturedHttp> Send(params (string Key, string? Value)[] settings)
    {
        using var handler = new CaptureHandler(); using var client = handler.Client();
        var service = new HttpV01ChatService(client, Samples.Config(settings), new LogBook().For<HttpV01ChatService>());
        await service.GenerateReplyAsync(Samples.ChatInput());
        return Assert.Single(handler.Requests);
    }
}
