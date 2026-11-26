#if PHASE2_GATEWAY_TESTS
// SPDX-License-Identifier: MIT
using LineBot.Api.Services;
using LineBot.Api.Workers;
using LineBot.Api.Tests.Support;
using Xunit.Abstractions;

namespace LineBot.Api.Tests.InternalContracts.Boundaries;

public sealed class Phase2IdempotencyBoundaryTests(ITestOutputHelper output) : DiagnosticTest(output)
{
    [Fact(DisplayName = "Common-Internal-17-01: Public idempotency interface is supplied to Worker")]
    [Trait("Case", "Common-Internal-17-01")]
    public void Declaration_UsesPublicInterface()
    {
        var boundary = typeof(IIdempotencyService);
        Trace("Common-Internal-17-01", new { Assembly = boundary.Assembly.FullName, Type = boundary.FullName,
            ExpectedMethods = new[] { "bool IsProcessed(string)", "void MarkAsProcessed(string)" },
            ConstructorOwner = typeof(MessageWorkerService).FullName });
        Assert.True(boundary.IsPublic && boundary.IsInterface);
        foreach (var operation in new[] { ("IsProcessed", typeof(bool)), ("MarkAsProcessed", typeof(void)) })
        {
            var method = Assert.Single(boundary.GetMethods(), m => m.Name == operation.Item1);
            Assert.Equal(operation.Item2, method.ReturnType);
            Assert.Equal(new[] { typeof(string) }, method.GetParameters().Select(p => p.ParameterType));
        }
        Assert.Contains(typeof(MessageWorkerService).GetConstructors(),
            c => c.GetParameters().Any(p => p.ParameterType == boundary));
    }
}
#endif
