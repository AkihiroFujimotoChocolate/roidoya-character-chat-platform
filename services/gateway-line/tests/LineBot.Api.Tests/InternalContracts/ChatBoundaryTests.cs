// SPDX-License-Identifier: MIT
using LineBot.Api.Models;
using LineBot.Api.Services;
using LineBot.Api.Tests.Support;
namespace LineBot.Api.Tests.InternalContracts;

public sealed class ChatBoundaryTests
{
    [Fact, Trait("Case", "Internal-01")]
    public void Input_contract_is_semantic_and_does_not_expose_version_or_transport()
    {
        var method = typeof(IChatService).GetMethod(nameof(IChatService.GenerateReplyAsync))!;
        Assert.Equal(typeof(ChatServiceRequest), method.GetParameters()[0].ParameterType);
        Assert.Equal(typeof(CancellationToken), method.GetParameters()[1].ParameterType);
        foreach (var property in typeof(ChatServiceRequest).GetProperties()) {
            Assert.DoesNotContain(property.Name, new[] { "ApiVersion", "Version", "MessageLanguage", "HttpRequest", "Headers", "Url" });
            Assert.DoesNotContain(CompiledDependencies.Expand(property.PropertyType), IsTransportOrWire);
        }
    }

    [Fact, Trait("Case", "Internal-02")]
    public void Result_exposes_messages_and_status_without_unused_wire_metadata()
    {
        Assert.Equal(typeof(Task<ChatServiceResult>),
            typeof(IChatService).GetMethod(nameof(IChatService.GenerateReplyAsync))!.ReturnType);
        Assert.NotNull(typeof(ChatServiceResult).GetProperty(nameof(ChatServiceResult.Messages)));
        Assert.NotNull(typeof(ChatServiceResult).GetProperty(nameof(ChatServiceResult.Status)));
        foreach (var property in typeof(ChatServiceResult).GetProperties()) {
            Assert.DoesNotContain(property.Name, new[] { "RequestId", "FallbackUsed", "Error" });
            Assert.DoesNotContain(CompiledDependencies.Expand(property.PropertyType), IsTransportOrWire);
        }
    }

    [Fact, Trait("Case", "Internal-03")]
    public void Worker_compiled_dependencies_use_abstract_services_without_wire_interpretation()
    {
        var types = CompiledDependencies.Types(typeof(MessageWorkerService)).Distinct().ToArray();
        Assert.Contains(typeof(IChatService), types);
        Assert.Contains(typeof(ILineReplyService), types);
        Assert.DoesNotContain(types, IsTransportOrWire);
        Assert.DoesNotContain(typeof(HttpV01ChatService), types);
        var uses = CompiledDependencies.Inspect(typeof(MessageWorkerService)).ToArray();
        Assert.DoesNotContain(uses, u => u.Literal is "Chat:ApiVersion" or "ok" or "provider_error" or "content_filtered");
        Assert.DoesNotContain(uses, u => u.Literal?.StartsWith("Chat:Http:", StringComparison.Ordinal) == true);
    }

    [Fact, Trait("Case", "Internal-04")]
    public void V01_implementation_owns_wire_conversion_and_interface_has_its_own_file()
    {
        Assert.True(typeof(IChatService).IsAssignableFrom(typeof(HttpV01ChatService)));
        Assert.Contains("V01", typeof(HttpV01ChatService).Name, StringComparison.OrdinalIgnoreCase);
        var source = Path.Combine(CompiledDependencies.SourceRoot(), "services", "gateway-line", "src",
            "LineBot.Api", "Services", "IChatService.cs");
        Assert.True(File.Exists(source), "IChatService must remain in its dedicated source file.");
        Assert.Matches(@"(?m)^\s*public\s+interface\s+IChatService\b", File.ReadAllText(source));
        var adapterTypes = CompiledDependencies.Types(typeof(HttpV01ChatService)).ToArray();
        Assert.Contains(adapterTypes, t => t == typeof(System.Text.Json.JsonSerializer));
        Assert.Contains(adapterTypes, t => t == typeof(HttpClient));
        Assert.DoesNotContain(typeof(LineReplyService), adapterTypes);
        Assert.DoesNotContain(typeof(ILineReplyService), adapterTypes);
    }

    private static bool IsTransportOrWire(Type type) =>
        type.Namespace?.StartsWith("System.Net.Http", StringComparison.Ordinal) == true ||
        type.Namespace?.StartsWith("System.Text.Json", StringComparison.Ordinal) == true ||
        type == typeof(Uri) || type.Name.Contains("Wire", StringComparison.OrdinalIgnoreCase) ||
        type == typeof(HttpV01ChatService);
}
