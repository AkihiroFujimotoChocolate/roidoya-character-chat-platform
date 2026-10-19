// SPDX-License-Identifier: MIT
using LineBot.Api.Tests.Support;
using Xunit.Abstractions;

namespace LineBot.Api.Tests.ExternalContracts.V01.Support;

public abstract class ExternalFlowTest : DiagnosticTest
{
    protected ExternalFlowTest(ITestOutputHelper output) : base(output) { }
    // Record exact bytes before starting the application or making the request.
    private protected void Record(string id, GatewayApplicationFactory host, ExternalValues input,
        byte[] bytes, string? signature, object scenario) =>
        Trace(id, new { Values = input, BodyBase64 = Convert.ToBase64String(bytes),
            Signature = signature, Settings = host.Settings, Scenario = scenario,
            WaitLimitSeconds = 10, QuietWindowMilliseconds = 250,
            NormalChatMetadata = new { request_id = "correlation-test", fallback_used = false },
            NormalLineResponse = "{\"sentMessages\":[{\"id\":\"sent-test\",\"quoteToken\":\"quote-test\"}]}" });

}
