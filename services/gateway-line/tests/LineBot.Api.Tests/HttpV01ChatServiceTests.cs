// SPDX-License-Identifier: MIT
using System.Net;
using System.Text;
using System.Text.Json;
using LineBot.Api.Models;
using LineBot.Api.Services;

namespace LineBot.Api.Tests;

public class HttpV01ChatServiceTests
{
    [Fact]
    public async Task GenerateReplyAsync_SendsV01UrlHeaderAndWireRequestShape_WithLanguageNull()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"request_id\":\"r1\",\"status\":\"ok\",\"messages\":[\"hello\"]}", Encoding.UTF8, "application/json")
        }));
        var service = CreateService(handler);

        var result = await service.GenerateReplyAsync(new ChatServiceRequest
        {
            RequestId = "req-1",
            MessageText = "hello",
            TimeoutSeconds = 20,
            MaxCharsPerMessage = 1000,
            ConversationId = "user-1",
            AuthorUserId = "user-1"
        });

        Assert.Equal("ok", result.Status);
        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("https://example.com/chat/v0.1/generate-replies", handler.LastRequest.RequestUri!.ToString());
        Assert.Equal("0.1", handler.LastRequest.Headers.GetValues("X-Chat-Api-Version").Single());

        var payload = await handler.LastRequest.Content!.ReadAsStringAsync();
        using var jsonDoc = JsonDocument.Parse(payload);
        var root = jsonDoc.RootElement;
        Assert.Equal("req-1", root.GetProperty("request_id").GetString());
        Assert.Equal("", root.GetProperty("event_id").GetString());
        Assert.Equal("line", root.GetProperty("origin").GetProperty("platform").GetString());
        Assert.Equal("user-1", root.GetProperty("conversation").GetProperty("id").GetString());
        Assert.Equal("line-user-1", root.GetProperty("author").GetProperty("user_id").GetString());
        Assert.Equal("hello", root.GetProperty("message").GetProperty("text").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("message").GetProperty("language").ValueKind);
        Assert.Equal(20, root.GetProperty("limits").GetProperty("timeout_seconds").GetInt32());
        Assert.Equal(1000, root.GetProperty("limits").GetProperty("max_chars_per_message").GetInt32());
    }

    [Fact]
    public async Task GenerateReplyAsync_MapsValidV01Response()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"status\":\"ok\",\"messages\":[\"m1\",\"m2\"]}", Encoding.UTF8, "application/json")
        }));
        var service = CreateService(handler);

        var result = await service.GenerateReplyAsync(new ChatServiceRequest { MessageText = "hello", ConversationId = "c", AuthorUserId = "a" });

        Assert.Equal("ok", result.Status);
        Assert.Equal(new[] { "m1", "m2" }, result.Messages);
    }

    [Theory]
    [InlineData("provider_error", "default-fallback")]
    [InlineData("content_filtered", "filtered-fallback")]
    public async Task GenerateReplyAsync_UsesFallbackForProviderErrorAndContentFiltered(string status, string expectedMessage)
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent($"{{\"status\":\"{status}\",\"messages\":[\"provider\"]}}", Encoding.UTF8, "application/json")
        }));
        var service = CreateService(handler, new Dictionary<string, string?>
        {
            ["Chat:Fallbacks:Default"] = "default-fallback",
            ["Chat:Fallbacks:ContentFiltered"] = "filtered-fallback"
        });

        var result = await service.GenerateReplyAsync(new ChatServiceRequest { MessageText = "hello", ConversationId = "c", AuthorUserId = "a" });

        Assert.Equal(status, result.Status);
        Assert.Equal(new[] { expectedMessage }, result.Messages);
    }

    [Fact]
    public async Task GenerateReplyAsync_PreservesBaselineBlankContentFilteredFallbackBehavior()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"status\":\"content_filtered\",\"messages\":[\"provider\"]}", Encoding.UTF8, "application/json")
        }));
        var service = CreateService(handler, new Dictionary<string, string?>
        {
            ["Chat:Fallbacks:Default"] = "default-fallback",
            ["Chat:Fallbacks:ContentFiltered"] = ""
        });

        var result = await service.GenerateReplyAsync(new ChatServiceRequest { MessageText = "hello", ConversationId = "c", AuthorUserId = "a" });

        Assert.Equal("content_filtered", result.Status);
        Assert.Equal(new[] { "" }, result.Messages);
    }

    [Fact]
    public async Task GenerateReplyAsync_ReturnsInvalidResponseFallbackForMalformedJson()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{", Encoding.UTF8, "application/json")
        }));
        var service = CreateService(handler, new Dictionary<string, string?>
        {
            ["Chat:Fallbacks:Default"] = "default-fallback"
        });

        var result = await service.GenerateReplyAsync(new ChatServiceRequest { RequestId = "req-2", MessageText = "hello", ConversationId = "c", AuthorUserId = "a" });

        Assert.Equal("provider_error", result.Status);
        Assert.Equal(new[] { "default-fallback" }, result.Messages);
    }

    [Fact]
    public async Task GenerateReplyAsync_ReturnsInvalidResponseFallbackForNullBody()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("null", Encoding.UTF8, "application/json")
        }));
        var service = CreateService(handler, new Dictionary<string, string?>
        {
            ["Chat:Fallbacks:Default"] = "default-fallback"
        });

        var result = await service.GenerateReplyAsync(new ChatServiceRequest { RequestId = "req-2", MessageText = "hello", ConversationId = "c", AuthorUserId = "a" });

        Assert.Equal("provider_error", result.Status);
        Assert.Equal(new[] { "default-fallback" }, result.Messages);
    }

    [Fact]
    public async Task GenerateReplyAsync_DoesNotNormalizeNullMessagesInSuccessfulResponse()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"status\":\"ok\",\"messages\":null}", Encoding.UTF8, "application/json")
        }));
        var service = CreateService(handler);

        var result = await service.GenerateReplyAsync(new ChatServiceRequest { MessageText = "hello", ConversationId = "c", AuthorUserId = "a" });

        Assert.Equal("ok", result.Status);
        Assert.Null(result.Messages);
    }

    [Fact]
    public async Task GenerateReplyAsync_ReturnsProviderErrorFallbackForHttpNonSuccess()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = new StringContent("bad gateway", Encoding.UTF8, "text/plain")
        }));
        var service = CreateService(handler, new Dictionary<string, string?>
        {
            ["Chat:Fallbacks:Default"] = "default-fallback"
        });

        var result = await service.GenerateReplyAsync(new ChatServiceRequest { MessageText = "hello", ConversationId = "c", AuthorUserId = "a" });

        Assert.Equal("provider_error", result.Status);
        Assert.Equal(new[] { "default-fallback" }, result.Messages);
    }

    [Fact]
    public async Task GenerateReplyAsync_ReturnsTimeoutFallbackWhenRequestTimesOut()
    {
        var handler = new StubHttpMessageHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var service = CreateService(handler, new Dictionary<string, string?>
        {
            ["Chat:RequestTimeoutSeconds"] = "1",
            ["Chat:Fallbacks:Timeout"] = "timeout-fallback"
        });

        var result = await service.GenerateReplyAsync(new ChatServiceRequest { RequestId = "req-3", MessageText = "hello", ConversationId = "c", AuthorUserId = "a" });

        Assert.Equal("provider_error", result.Status);
        Assert.Equal(new[] { "timeout-fallback" }, result.Messages);
    }

    [Fact]
    public async Task GenerateReplyAsync_ThrowsWhenCallerCancels()
    {
        var handler = new StubHttpMessageHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var service = CreateService(handler, new Dictionary<string, string?>
        {
            ["Chat:RequestTimeoutSeconds"] = "20"
        });

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.GenerateReplyAsync(new ChatServiceRequest { MessageText = "hello", ConversationId = "c", AuthorUserId = "a" }, cts.Token));
    }

    [Fact]
    public void Constructor_RejectsUnsupportedConfiguredVersion()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var configuration = TestSupport.BuildConfiguration(new Dictionary<string, string?>
        {
            ["Chat:ApiVersion"] = "0.2",
            ["Chat:Http:BaseUrl"] = "https://example.com"
        });

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new HttpV01ChatService(new HttpClient(handler), configuration, TestSupport.Logger<HttpV01ChatService>()));

        Assert.Contains("Unsupported Chat:ApiVersion '0.2'", exception.Message);
    }

    [Fact]
    public async Task GenerateReplyAsync_UsesEndpointLiteralOverride_WhenConfigured()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"status\":\"ok\",\"messages\":[\"hello\"]}", Encoding.UTF8, "application/json")
        }));
        var service = CreateService(handler, new Dictionary<string, string?>
        {
            ["Chat:Http:Endpoint"] = "https://override.example/custom/path",
            ["Chat:Http:EndpointTemplate"] = "/chat/{version}/ignored",
            ["Chat:Http:BaseUrl"] = "https://base.example"
        });

        await service.GenerateReplyAsync(new ChatServiceRequest { MessageText = "hello", ConversationId = "c", AuthorUserId = "a" });

        Assert.Equal("https://override.example/custom/path", handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task GenerateReplyAsync_UsesLiteralTemplateWhenVersionPlaceholderMissing()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"status\":\"ok\",\"messages\":[\"hello\"]}", Encoding.UTF8, "application/json")
        }));
        var service = CreateService(handler, new Dictionary<string, string?>
        {
            ["Chat:Http:Endpoint"] = "",
            ["Chat:Http:EndpointTemplate"] = "/custom/literal/path",
            ["Chat:Http:BaseUrl"] = "https://base.example"
        });

        await service.GenerateReplyAsync(new ChatServiceRequest { MessageText = "hello", ConversationId = "c", AuthorUserId = "a" });

        Assert.Equal("https://base.example/custom/literal/path", handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task GenerateReplyAsync_UsesDefaultV01PathWhenEndpointAndTemplateMissing()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"status\":\"ok\",\"messages\":[\"hello\"]}", Encoding.UTF8, "application/json")
        }));
        var service = CreateService(handler, new Dictionary<string, string?>
        {
            ["Chat:Http:Endpoint"] = "",
            ["Chat:Http:EndpointTemplate"] = "",
            ["Chat:Http:BaseUrl"] = "https://base.example"
        });

        await service.GenerateReplyAsync(new ChatServiceRequest { MessageText = "hello", ConversationId = "c", AuthorUserId = "a" });

        Assert.Equal("https://base.example/chat/v0.1/generate-replies", handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task GenerateReplyAsync_UsesAppBaseUrlWhenBaseUrlMissing()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"status\":\"ok\",\"messages\":[\"hello\"]}", Encoding.UTF8, "application/json")
        }));
        var service = CreateService(handler, new Dictionary<string, string?>
        {
            ["Chat:Http:BaseUrl"] = "",
            ["App:BaseUrl"] = "https://appbase.example",
            ["Chat:Http:EndpointTemplate"] = "/chat/{version}/generate-replies"
        });

        await service.GenerateReplyAsync(new ChatServiceRequest { MessageText = "hello", ConversationId = "c", AuthorUserId = "a" });

        Assert.Equal("https://appbase.example/chat/v0.1/generate-replies", handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task GenerateReplyAsync_UsesAdditionalHeadersViaRequestHeadersOnly()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"status\":\"ok\",\"messages\":[\"hello\"]}", Encoding.UTF8, "application/json")
        }));
        var service = CreateService(handler, new Dictionary<string, string?>
        {
            ["Chat:Http:AdditionalHeaders"] = "{\"X-Test-Header\":\"abc\"}"
        });

        await service.GenerateReplyAsync(new ChatServiceRequest { MessageText = "hello", ConversationId = "c", AuthorUserId = "a" });

        Assert.Equal("abc", handler.LastRequest!.Headers.GetValues("X-Test-Header").Single());
    }

    [Fact]
    public async Task GenerateReplyAsync_InvalidAdditionalHeaderIsIgnored_BaselineBehavior()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"status\":\"ok\",\"messages\":[\"hello\"]}", Encoding.UTF8, "application/json")
        }));
        var service = CreateService(handler, new Dictionary<string, string?>
        {
            ["Chat:Http:AdditionalHeaders"] = "{\"Content-Type\":\"application/json\"}",
            ["Chat:Fallbacks:Default"] = "default-fallback"
        });

        var result = await service.GenerateReplyAsync(new ChatServiceRequest { MessageText = "hello", ConversationId = "c", AuthorUserId = "a" });

        Assert.Equal("ok", result.Status);
        Assert.Equal(new[] { "hello" }, result.Messages);
    }

    private static HttpV01ChatService CreateService(StubHttpMessageHandler handler, Dictionary<string, string?>? extra = null)
    {
        var configValues = new Dictionary<string, string?>
        {
            ["Chat:ApiVersion"] = "0.1",
            ["Chat:RequestTimeoutSeconds"] = "20",
            ["Chat:Http:BaseUrl"] = "https://example.com",
            ["App:BaseUrl"] = "",
            ["Chat:Http:EndpointTemplate"] = "/chat/{version}/generate-replies",
            ["Chat:Http:Endpoint"] = "",
            ["Chat:Http:VersionHeaderName"] = "X-Chat-Api-Version",
            ["Chat:Fallbacks:Default"] = "default-fallback",
            ["Chat:Fallbacks:ContentFiltered"] = "",
            ["Chat:Fallbacks:Timeout"] = ""
        };

        if (extra is not null)
        {
            foreach (var kvp in extra)
            {
                configValues[kvp.Key] = kvp.Value;
            }
        }

        return new HttpV01ChatService(new HttpClient(handler), TestSupport.BuildConfiguration(configValues), TestSupport.Logger<HttpV01ChatService>());
    }
}
