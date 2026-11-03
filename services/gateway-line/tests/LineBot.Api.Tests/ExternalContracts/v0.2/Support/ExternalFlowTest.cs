// SPDX-License-Identifier: MIT
using LineBot.Api.Tests.Support;
using Xunit.Abstractions;

namespace LineBot.Api.Tests.ExternalContracts.V02.Support;

public abstract class ExternalFlowTest : DiagnosticTest
{
    protected ExternalFlowTest(ITestOutputHelper output) : base(output) { }
    // Record exact bytes before starting the application or making the request.
    private protected void Record(string id, GatewayApplicationFactory host, ExternalValues input,
        byte[] bytes, string? signature, object scenario, string? responseOutputId = null) =>
        Trace(id, new { Values = input, BodyBase64 = Convert.ToBase64String(bytes),
            Signature = signature, Settings = host.Settings, Scenario = scenario,
            WaitLimitSeconds = 10, QuietWindowMilliseconds = 250,
            NormalChatMetadata = new { interaction_id = "copy from captured request", status = "ok", OutputId = responseOutputId ?? input.OutputMessageId, Text = input.ReplyText, Messages = 1, TextParts = 1 },
            Expected = new { ChatMethod = "POST", ChatMediaType = "application/json", RootFields = new[] { "interaction_id", "origin", "actor", "character", "message" }, UuidIds = true, LineAuthorization = "Bearer access-token-test", LineMediaType = "application/json; charset=utf-8" },
            NormalLineResponse = "{\"sentMessages\":[{\"id\":\"sent-test\",\"quoteToken\":\"quote-test\"}]}" });

}
