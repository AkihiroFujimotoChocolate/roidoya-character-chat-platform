#if PHASE2_GATEWAY_TESTS
// SPDX-License-Identifier: MIT
using LineBot.Api.Tests.InternalContracts.Support;
using LineBot.Api.Chat;
using LineBot.Api.Processing;
using LineBot.Api.Tests.ExternalContracts.V01.Line;
using LineBot.Api.Tests.ExternalContracts.V02.ConnectionManagement;
using LineBot.Api.Tests.ExternalContracts.V02.Support;
using LineBot.Api.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Request = LineBot.Api.Contracts.V0_2.ChatServiceRequest;

namespace LineBot.Api.Tests.InternalContracts.Support.V02;

// c0.2 request construction belongs with the version-specific fixture.
internal static class Phase2ChatInputs
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
        var assembler = scope.ServiceProvider.GetRequiredKeyedService<IInputAssembler<Request>>("0.2");
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

    internal static Request ChatV02(this Phase2FlowValues data) => new()
    {
        InteractionId = data.InteractionId, ChannelType = "line", ChannelInstanceId = data.Destination,
        ActorId = data.ActorId, CharacterId = data.CharacterId, InputMessageId = data.InputMessageId, MessageText = data.Text
    };
}
#else
// SPDX-License-Identifier: MIT
using LineBot.Api.Models;
using LineBot.Api.Services;
using LineBot.Api.Tests.Support;

namespace LineBot.Api.Tests.InternalContracts.Support.V02;

internal sealed record FlowValues(string Text, string ConversationId, string PersonId, string EventId,
    string ReplyToken, string MessageId, string Destination, string[] Messages, string DefaultFallback,
    string InteractionId, string ChannelType, string ChannelInstanceId, string ActorId, string CharacterId, string InputMessageId)
{
    internal QueueItem Job(bool redelivery = false) {
        var job = new QueueItem {
        MessageText = Text, UserKey = ConversationId, WebhookEventId = EventId,
        ReplyToken = ReplyToken, IsRedelivery = redelivery
        };
        foreach (var name in new[] { "InteractionId", "ChannelType", "ChannelInstanceId", "ActorId", "CharacterId", "InputMessageId" })
            typeof(QueueItem).GetProperty(name)!.SetValue(job, GetType().GetProperty(name)!.GetValue(this));
        return job;
    }

}

internal static class FlowSamples
{
    internal static FlowValues CreateRandom(int count = 2, bool richMessages = false, string? text = null)
    {
        if (count is < 1 or > 6) throw new ArgumentOutOfRangeException(nameof(count));
        return Create(new(text ?? GeneratedValues.Text(), GeneratedValues.Id("conversation"),
            GeneratedValues.Id("person"), GeneratedValues.Id("event"), GeneratedValues.Id("reply-token"),
            GeneratedValues.Id("message"), GeneratedValues.Id("destination"),
            Enumerable.Range(1, count).Select(i =>
                (richMessages ? "  こんにちは😀\n\"引用\"\\path  " : "reply") + ":" + i + ":" + GeneratedValues.Id("text") + (richMessages ? "  " : "")).ToArray(),
            GeneratedValues.Id("fallback"), Guid.NewGuid().ToString(), "line", GeneratedValues.Id("channel"),
            GeneratedValues.Id("actor"), GeneratedValues.Id("character"), Guid.NewGuid().ToString()));
    }
    internal static FlowValues Create(FlowValues values) => values with { Messages = values.Messages.ToArray() };
    internal static FlowValues FromJson(string valuesJson) =>
        Create(GeneratedValues.Read<FlowValues>(valuesJson, "Text", "ConversationId", "PersonId",
            "EventId", "ReplyToken", "MessageId", "Destination", "Messages", "DefaultFallback", "InteractionId", "ChannelType", "ChannelInstanceId", "ActorId", "CharacterId", "InputMessageId"));
}
#endif
