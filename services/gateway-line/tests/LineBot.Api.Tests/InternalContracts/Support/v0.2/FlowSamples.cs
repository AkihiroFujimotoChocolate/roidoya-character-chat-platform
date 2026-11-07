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
