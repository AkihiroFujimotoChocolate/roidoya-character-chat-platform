#if PHASE2_GATEWAY_TESTS
// SPDX-License-Identifier: MIT
using LineBot.Api.Tests.InternalContracts.Support;
using LineBot.Api.Chat;
using LineBot.Api.Processing;
using LineBot.Api.Tests.ExternalContracts.V01.Line;
using LineBot.Api.Tests.ExternalContracts.V01.ConnectionManagement;
using LineBot.Api.Tests.ExternalContracts.V01.Support;
using LineBot.Api.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Request = LineBot.Api.Contracts.V0_1.ChatServiceRequest;

namespace LineBot.Api.Tests.InternalContracts.Support.V01;

// c0.1 request construction belongs with the version-specific fixture.
internal static class Phase2ChatInputsC01
{
    internal static async Task<LineReplyContext> PrepareReplyContextAsync(this Phase2FlowValues data)
    {
        var connection = ExternalSamples.Random() with { ConnectionId = data.ConnectionId!, ActorId = data.ActorId,
            CharacterId = data.CharacterId, PersonId = data.PersonId, Destination = data.Destination };
        using var connections = ConnectionSamples.Capture(connection);
        using var unused = new CaptureHandler((_, _) => throw new InvalidOperationException("Unexpected request."));
        using var host = new GatewayApplicationFactory(unused, unused, connections);
        using var scope = host.Services.CreateScope();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var assembler = scope.ServiceProvider.GetRequiredKeyedService<IInputAssembler<Request>>("0.1");
        var result = await assembler.PrepareAsync(data.Job(), deadline.Token).WaitAsync(deadline.Token);
        return Assert.IsType<LineReplyContext>(result.ReplyContext);
    }

    internal static async Task AssertReplyTokensAsync(LineReplyContext context, string expectedReplyToken)
    {
        using var line = new CaptureHandler((_, _) => Task.FromResult(LineReplySamples.SuccessResponse()));
        using var unused = new CaptureHandler((_, _) => throw new InvalidOperationException("Unexpected request."));
        using var host = new GatewayApplicationFactory(unused, line, unused);
        using var scope = host.Services.CreateScope();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var reply = scope.ServiceProvider.GetRequiredService<ILineReplyService>();
        Assert.True(await reply.SendReplyAsync(context, ["test"], deadline.Token).WaitAsync(deadline.Token));
        var sent = Assert.Single(line.Requests);
        using var body = sent.Json();
        Assert.Equal(expectedReplyToken, body.RootElement.GetProperty("replyToken").GetString());
        Assert.Equal("Bearer " + LineReplySamples.AccessToken, sent.Header("Authorization"));
    }

    internal static Request ChatV01(this Phase2FlowValues data) => new()
    {
        RequestId = data.InteractionId, ConversationId = "user-" + data.PersonId,
        AuthorUserId = "user-" + data.PersonId, MessageText = data.Text, TimeoutSeconds = 20, MaxCharsPerMessage = 1000
    };
}

#endif
