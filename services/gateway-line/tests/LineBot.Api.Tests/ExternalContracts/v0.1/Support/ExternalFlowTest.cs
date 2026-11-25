#if PHASE2_GATEWAY_TESTS
// SPDX-License-Identifier: MIT
using LineBot.Api.Tests.Support;
using Xunit.Abstractions;
using LineBot.Api.Tests.ExternalContracts.V01.ConnectionManagement;
using LineBot.Api.Tests.ExternalContracts.V01.Line;

namespace LineBot.Api.Tests.ExternalContracts.V01.Support;

public abstract class ExternalFlowTest : DiagnosticTest
{
    protected ExternalFlowTest(ITestOutputHelper output) : base(output) { }
    // Record exact bytes before starting the application or making the request.
    private protected void Record(string id, GatewayApplicationFactory host, ExternalValues input,
        byte[] bytes, string? signature, object scenario, ExternalValues? chatResponse = null, string? connectionDestination = null, ExternalValues? connection = null) =>
        Trace(id, new { Values = input, BodyBase64 = Convert.ToBase64String(bytes),
            Signature = signature, OutboundResponseStatus = 200, OutboundResponseContentType = "application/json; charset=utf-8", Path = "/line/webhook",
            ConnectionResponse = ConnectionSamples.Connection(connection ?? input, connectionDestination),
            Settings = host.Settings, Scenario = scenario,
            WaitLimitSeconds = 10, QuietWindowMilliseconds = 250,
            NormalChatMetadata = new { request_id = "correlation-test", status = "ok", Messages = new[] { (chatResponse ?? input).ReplyText }, fallback_used = false },
            Expected = new { ChatUrl = host.ChatUri, GetConnectionUrl = host.GetUri, ResolveActorUrl = host.ActorUri,
                LineUrl = "https://api.line.me/v2/bot/message/reply", ConnectionMethod = "POST",
                ConnectionMediaType = "application/json", ChatMethod = "POST", ChatMediaType = "application/json", RootFields = new[] { "request_id", "event_id", "origin", "conversation", "author", "message", "limits" }, LineAuthorization = "Bearer access-token-test", LineMediaType = "application/json; charset=utf-8" },
            NormalLineResponse = LineReplySamples.SuccessResponseJson });

}

#else
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

#endif
