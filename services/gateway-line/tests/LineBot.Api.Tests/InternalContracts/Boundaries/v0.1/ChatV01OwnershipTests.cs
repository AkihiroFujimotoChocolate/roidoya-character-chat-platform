#if PHASE2_GATEWAY_TESTS
// SPDX-License-Identifier: MIT
using LineBot.Api.Chat;
using LineBot.Api.Processing;
using LineBot.Api.Tests.InternalContracts.Boundaries.Support;
using LineBot.Api.Tests.Support;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit.Abstractions;
using Request = LineBot.Api.Contracts.V0_1.ChatServiceRequest;

namespace LineBot.Api.Tests.InternalContracts.Boundaries.V01;

public sealed class ChatV01OwnershipTests(ITestOutputHelper output) : DiagnosticTest(output)
{
    [Fact(DisplayName = "C01-Internal-09-01: Adapter owns Chat conversion and interface has its own file")]
    [Trait("Case", "C01-Internal-09")]
    public void V01Adapter_Dependencies_OwnWireConversion()
    {
        var assembly = typeof(global::Program).Assembly;
        var source = Path.Combine(CompiledDependencies.SourceRoot(), "services", "gateway-line", "src", "LineBot.Api");
        Trace("C01-Internal-09-01", new { Assembly = assembly.FullName, Project = source, Adapter = "Contracts.V0_1.HttpChatService" });
        var adapter = assembly.GetType("LineBot.Api.Contracts.V0_1.HttpChatService", throwOnError: true)!;
        Assert.Equal(typeof(Request).Namespace, adapter.Namespace);
        Assert.True(typeof(IChatService<Request>).IsAssignableFrom(adapter));
        var dependencies = CompiledDependencies.Types(adapter).ToArray();
        Assert.DoesNotContain(typeof(ILineReplyService), dependencies);
        Assert.DoesNotContain(dependencies, type => type.Name == "LineReplyService" && !type.IsInterface);

        var declarations = Directory.EnumerateFiles(source, "*.cs", SearchOption.AllDirectories)
            .Where(path => !Path.GetRelativePath(source, path).Split(Path.DirectorySeparatorChar).Any(segment => segment is "bin" or "obj"))
            .Select(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path)).GetRoot().DescendantNodes().OfType<BaseTypeDeclarationSyntax>().ToArray());
        var file = Assert.Single(declarations, types => types.OfType<InterfaceDeclarationSyntax>()
            .Any(type => type.Identifier.ValueText == "IChatService" && type.TypeParameterList?.Parameters.Count == 1));
        Assert.IsType<InterfaceDeclarationSyntax>(Assert.Single(file));
    }
}

#else
// SPDX-License-Identifier: MIT
using LineBot.Api.Services;
using LineBot.Api.Tests.InternalContracts.Boundaries.Support;

namespace LineBot.Api.Tests.InternalContracts.Boundaries.V01;

public sealed class ChatV01OwnershipTests
{
    [Fact(DisplayName = "Internal-04: The v0.1 adapter owns wire conversion and the interface has its own file")]
    [Trait("Case", "Internal-04")]
    public void V01Adapter_Dependencies_OwnWireConversion()
    {
        // Arrange / Act / Assert: inspect the compiled type and source ownership.
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

}

#endif
