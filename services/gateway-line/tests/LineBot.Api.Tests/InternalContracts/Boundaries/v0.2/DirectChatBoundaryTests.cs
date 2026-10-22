// SPDX-License-Identifier: MIT
using LineBot.Api.Services;
using LineBot.Api.Tests.InternalContracts.Boundaries.Support;

namespace LineBot.Api.Tests.InternalContracts.Boundaries.V02;

public sealed class DirectChatBoundaryTests
{
    [Fact(DisplayName = "V02-Internal-01: The c0.2 adapter owns HTTP and wire conversion without leaking into Worker")]
    [Trait("Case", "V02-Internal-01")]
    public void Adapter_OwnsWireAndWorkerRemainsVersionIndependent()
    {
        var adapter = typeof(IChatService).Assembly.GetType("LineBot.Api.Services.HttpV02ChatService");
        Assert.NotNull(adapter);
        Assert.True(typeof(IChatService).IsAssignableFrom(adapter));
        // External HTTP tests verify wire behavior. Ownership checks allow adapter-side
        // helpers and serializer choices, while following application-owned dependencies.
        var adapterTypes = ApplicationDependencies(adapter!).Types;
        Assert.DoesNotContain(typeof(ILineReplyService), adapterTypes);
        Assert.DoesNotContain(typeof(LineReplyService), adapterTypes);
        var workerDependencies = ApplicationDependencies(typeof(MessageWorkerService));
        var workerTypes = workerDependencies.Types;
        Assert.DoesNotContain(adapter!, workerTypes);
        Assert.DoesNotContain(workerTypes, t => t.Namespace?.StartsWith("System.Net.Http", StringComparison.Ordinal) == true ||
            t.Namespace?.StartsWith("System.Text.Json", StringComparison.Ordinal) == true ||
            t.Name.Contains("Wire", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(workerDependencies.Uses, u =>
            u.Literal is "Chat:ApiVersion" or "0.1" or "0.2" ||
            u.Literal?.StartsWith("Chat:Http:", StringComparison.Ordinal) == true);
    }

    private static (HashSet<Type> Types, List<CompiledDependencies.Use> Uses) ApplicationDependencies(Type root)
    {
        var types = new HashSet<Type>();
        var uses = new List<CompiledDependencies.Use>();
        var inspected = new HashSet<Type>();
        var pending = new Stack<Type>();
        pending.Push(root);
        while (pending.TryPop(out var owner)) {
            if (!inspected.Add(owner)) continue;
            uses.AddRange(CompiledDependencies.Inspect(owner));
            foreach (var type in CompiledDependencies.Types(owner)) {
                types.Add(type);
                if (type.Assembly == root.Assembly && !inspected.Contains(type)) pending.Push(type);
            }
        }
        return (types, uses);
    }
}
