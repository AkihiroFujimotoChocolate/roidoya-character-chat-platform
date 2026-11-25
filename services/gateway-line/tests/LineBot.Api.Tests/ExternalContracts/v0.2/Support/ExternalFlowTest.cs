#if PHASE2_GATEWAY_TESTS
// SPDX-License-Identifier: MIT
using LineBot.Api.Tests.Support;
using Xunit.Abstractions;
using LineBot.Api.Tests.ExternalContracts.V02.ConnectionManagement;
using LineBot.Api.Tests.ExternalContracts.V01.Line;

namespace LineBot.Api.Tests.ExternalContracts.V02.Support;

public abstract class ExternalFlowTest : DiagnosticTest
{
    protected ExternalFlowTest(ITestOutputHelper output) : base(output) { }
    // Record exact bytes before starting the application or making the request.
    private protected void Record(string id, GatewayApplicationFactory host, ExternalValues input,
        byte[] bytes, string? signature, object scenario, ExternalValues? chatResponse = null, string? connectionDestination = null, ExternalValues? connection = null) =>
        Trace(id, new { Values = input, BodyBase64 = Convert.ToBase64String(bytes),
            Signature = signature, OutboundResponseStatus = 200, OutboundResponseContentType = "application/json; charset=utf-8", Path = "/line/webhook/" + input.ConnectionId,
            ConnectionResponse = ConnectionSamples.Connection(connection ?? input, connectionDestination), ActorResponse = new { actor = new { id = input.ActorId } },
            Settings = host.Settings, Scenario = scenario,
            WaitLimitSeconds = 10, QuietWindowMilliseconds = 250,
            NormalChatMetadata = new { interaction_id = "copy from captured request", status = "ok", OutputId = (chatResponse ?? input).OutputMessageId, Text = (chatResponse ?? input).ReplyText, Messages = 1, TextParts = 1 },
            Expected = new { ChatUrl = host.ChatUri, GetConnectionUrl = host.GetUri, ResolveActorUrl = host.ActorUri,
                LineUrl = "https://api.line.me/v2/bot/message/reply", ConnectionMethod = "POST",
                ConnectionMediaType = "application/json", ChatMethod = "POST", ChatMediaType = "application/json", RootFields = new[] { "interaction_id", "origin", "actor", "character", "message" }, InteractionIdIsGuid = true, LineAuthorization = "Bearer access-token-test", LineMediaType = "application/json; charset=utf-8" },
            NormalLineResponse = LineReplySamples.SuccessResponseJson });

}
#else
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
#endif
