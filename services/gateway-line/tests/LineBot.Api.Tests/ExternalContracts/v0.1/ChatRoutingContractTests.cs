// SPDX-License-Identifier: MIT
using LineBot.Api.Services;
using LineBot.Api.Tests.Support;
using Microsoft.Extensions.Logging;
namespace LineBot.Api.Tests.ExternalContracts.V01;

public sealed class ChatRoutingContractTests
{
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

    [Theory]
    [InlineData("/api/chat/v0.1", "https://chat.test/api/chat/v0.1")]
    [InlineData("/literal/{version}", "https://chat.test/literal/%7Bversion%7D")]
    [Trait("Case", "External-06")]
    public async Task Explicit_endpoint_wins_and_is_not_a_template(string endpoint, string expected)
    {
        var sent = await Send(("Chat:Http:Endpoint", endpoint), ("Chat:Http:EndpointTemplate", "/chat/{version}/generate-replies"));
        Assert.Equal(new Uri(expected), sent.Uri);
    }

    [Fact, Trait("Case", "External-07")]
    public async Task Template_expands_the_protocol_version() =>
        Assert.Equal(new Uri("https://chat.test/chat/v0.1/generate-replies"),
            (await Send(("Chat:Http:EndpointTemplate", "/chat/{version}/generate-replies"))).Uri);

    [Fact, Trait("Case", "External-08")]
    public async Task Template_without_placeholder_is_literal() =>
        Assert.Equal(new Uri("https://chat.test/fixed/reply"),
            (await Send(("Chat:Http:EndpointTemplate", "/fixed/reply"))).Uri);

    [Fact, Trait("Case", "External-09")]
    public async Task Missing_overrides_use_the_default_path() =>
        Assert.Equal(new Uri("https://chat.test/chat/v0.1/generate-replies"), (await Send()).Uri);

    [Theory]
    [InlineData("Chat:Http:Endpoint")]
    [InlineData("Chat:Http:EndpointTemplate")]
    [Trait("Case", "External-10")]
    public async Task Absolute_override_does_not_add_a_base_url(string key) =>
        Assert.Equal(new Uri("https://absolute.test/direct"), (await Send((key, "https://absolute.test/direct"))).Uri);

    [Theory]
    [InlineData("https://chat.test/", "https://chat.test/reply")]
    [InlineData(null, "https://app.test/reply")]
    [Trait("Case", "External-11")]
    public async Task Relative_override_respects_base_url_priority(string? chatBase, string expected) =>
        Assert.Equal(new Uri(expected), (await Send(("Chat:Http:Endpoint", "/reply"),
            ("Chat:Http:BaseUrl", chatBase), ("App:BaseUrl", "https://app.test/"))).Uri);

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

    [Theory]
    [InlineData("/unversioned/chat")]
    [InlineData("/chat/v9/reply")]
    [Trait("Case", "External-13")]
    public async Task Route_spelling_does_not_select_a_different_protocol(string endpoint)
    {
        var sent = await Send(("Chat:Http:Endpoint", endpoint));
        Assert.Equal(new Uri("https://chat.test" + endpoint), sent.Uri);
        Assert.Equal("0.1", sent.Header("X-Chat-Api-Version"));
        using var json = sent.Json(); var body = json.RootElement;
        Assert.Equal("line", body.GetProperty("origin").GetProperty("platform").GetString());
        Assert.Equal("conversation-test", body.GetProperty("conversation").GetProperty("id").GetString());
        Assert.Equal("line-author-test", body.GetProperty("author").GetProperty("user_id").GetString());
        Assert.Equal("こんにちは", body.GetProperty("message").GetProperty("text").GetString());
        Assert.False(body.TryGetProperty("version", out _)); Assert.False(body.TryGetProperty("operation", out _));
    }
    private static async Task<CapturedHttp> Send(params (string Key, string? Value)[] settings)
    {
        using var handler = new CaptureHandler(); using var client = handler.Client();
        var service = new HttpV01ChatService(client, Samples.Config(settings), new LogBook().For<HttpV01ChatService>());
        await service.GenerateReplyAsync(Samples.ChatInput());
        return Assert.Single(handler.Requests);
    }
}
