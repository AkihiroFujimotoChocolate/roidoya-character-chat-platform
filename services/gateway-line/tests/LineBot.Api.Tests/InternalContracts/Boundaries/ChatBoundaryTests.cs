// SPDX-License-Identifier: MIT
using LineBot.Api.Models;
using LineBot.Api.Services;
using LineBot.Api.Tests.InternalContracts.Boundaries.Support;
namespace LineBot.Api.Tests.InternalContracts.Boundaries;

public sealed class ChatBoundaryTests
{
    [Fact(DisplayName = "Internal-01: Chat input excludes protocol version and transport details")]
    [Trait("Case", "Internal-01")]
    public void ChatInput_Contract_ExcludesVersionAndTransport()
    {
        // Arrange
        var method = typeof(IChatService).GetMethod(nameof(IChatService.GenerateReplyAsync))!;
        // Assert
        Assert.Equal(typeof(ChatServiceRequest), method.GetParameters()[0].ParameterType);
        Assert.Equal(typeof(CancellationToken), method.GetParameters()[1].ParameterType);
        foreach (var property in typeof(ChatServiceRequest).GetProperties()) {
            Assert.DoesNotContain(property.Name, new[] { "ApiVersion", "Version", "MessageLanguage", "HttpRequest", "Headers", "Url" });
            Assert.DoesNotContain(CompiledDependencies.Expand(property.PropertyType), IsTransportOrWire);
        }
    }

    [Fact(DisplayName = "Internal-02: Chat result excludes unused wire metadata and transport details")]
    [Trait("Case", "Internal-02")]
    public void ChatResult_Contract_ExcludesUnusedWireMetadata()
    {
        // Assert
        Assert.Equal(typeof(Task<ChatServiceResult>),
            typeof(IChatService).GetMethod(nameof(IChatService.GenerateReplyAsync))!.ReturnType);
        Assert.NotNull(typeof(ChatServiceResult).GetProperty(nameof(ChatServiceResult.Messages)));
        Assert.NotNull(typeof(ChatServiceResult).GetProperty(nameof(ChatServiceResult.Status)));
        foreach (var property in typeof(ChatServiceResult).GetProperties()) {
            Assert.DoesNotContain(property.Name, new[] { "RequestId", "FallbackUsed", "Error" });
            Assert.DoesNotContain(CompiledDependencies.Expand(property.PropertyType), IsTransportOrWire);
        }
    }

    [Fact(DisplayName = "Internal-03: Worker depends on abstract services without interpreting wire details")]
    [Trait("Case", "Internal-03")]
    public void Worker_CompiledDependencies_UseAbstractBoundaries()
    {
        // Arrange / Act
        var types = CompiledDependencies.Types(typeof(MessageWorkerService)).Distinct().ToArray();
        // Assert
        Assert.Contains(typeof(IChatService), types);
        Assert.Contains(typeof(ILineReplyService), types);
        Assert.DoesNotContain(types, IsTransportOrWire);
        Assert.DoesNotContain(typeof(HttpV01ChatService), types);
        var uses = CompiledDependencies.Inspect(typeof(MessageWorkerService)).ToArray();
        Assert.DoesNotContain(uses, u => u.Literal is "Chat:ApiVersion" or "ok" or "provider_error" or "content_filtered");
        Assert.DoesNotContain(uses, u => u.Literal?.StartsWith("Chat:Http:", StringComparison.Ordinal) == true);
    }

    private static bool IsTransportOrWire(Type type) =>
        type.Namespace?.StartsWith("System.Net.Http", StringComparison.Ordinal) == true ||
        type.Namespace?.StartsWith("System.Text.Json", StringComparison.Ordinal) == true ||
        type == typeof(Uri) || type.Name.Contains("Wire", StringComparison.OrdinalIgnoreCase) ||
        type == typeof(HttpV01ChatService);
}

