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
