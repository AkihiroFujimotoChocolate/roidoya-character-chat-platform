// SPDX-License-Identifier: MIT
using LineBot.Api.Services;
using LineBot.Api.Tests.InternalContracts.Boundaries.Support;

namespace LineBot.Api.Tests.InternalContracts.Boundaries.V02;

public sealed class ChatV02OwnershipTests
{
    [Fact(DisplayName = "C02-Internal-09: The v0.2 adapter owns wire conversion and the interface has its own file")]
    [Trait("Case", "C02-Internal-09")]
    public void V02Adapter_Dependencies_OwnWireConversion()
    {
        // Arrange / Act / Assert: inspect the compiled type and source ownership.
        Assert.True(typeof(IChatService).IsAssignableFrom(typeof(IChatService).Assembly.GetType("LineBot.Api.Services.HttpV02ChatService", throwOnError: true)!));
        Assert.Contains("V02", typeof(IChatService).Assembly.GetType("LineBot.Api.Services.HttpV02ChatService", throwOnError: true)!.Name, StringComparison.OrdinalIgnoreCase);
        var source = Path.Combine(CompiledDependencies.SourceRoot(), "services", "gateway-line", "src",
            "LineBot.Api", "Services", "IChatService.cs");
        Assert.True(File.Exists(source), "IChatService must remain in its dedicated source file.");
        Assert.Matches(@"(?m)^\s*public\s+interface\s+IChatService\b", File.ReadAllText(source));
        var adapterTypes = CompiledDependencies.Types(typeof(IChatService).Assembly.GetType("LineBot.Api.Services.HttpV02ChatService", throwOnError: true)!).ToArray();
        Assert.DoesNotContain(typeof(LineReplyService), adapterTypes);
        Assert.DoesNotContain(typeof(ILineReplyService), adapterTypes);
    }

}
