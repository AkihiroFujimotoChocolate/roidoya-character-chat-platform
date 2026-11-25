#if PHASE2_GATEWAY_TESTS
// SPDX-License-Identifier: MIT
using System.Reflection;
using System.Text.Json;
using LineBot.Api.Chat;
using LineBot.Api.Processing;
using LineBot.Api.Queueing;
using LineBot.Api.Tests.ExternalContracts.V01.Line;
using LineBot.Api.Tests.Support;

namespace LineBot.Api.Tests.InternalContracts.Support;

internal sealed record Phase2FlowValues(string Text, string PersonId, string EventId, string ReplyToken,
    string MessageId, string Destination, string? ConnectionId, string ActorId, string CharacterId,
    string InteractionId, string InputMessageId, string ReplyText, string[] Messages)
{
    internal QueueItem Job()
    {
        var body = LineWebhookSamples.Webhook(text: Text, person: PersonId, eventId: EventId, replyToken: ReplyToken,
            messageId: MessageId, destination: Destination);
        using var json = JsonDocument.Parse(body);
        var webhook = JsonSerializer.SerializeToElement(new { destination = Destination, @event = json.RootElement.GetProperty("events")[0] });
        return (QueueItem)Phase2FlowSamples.Create(typeof(QueueItem), ("Webhook", webhook), ("ConnectionId", ConnectionId));
    }

}

internal static class Phase2FlowSamples
{
    internal static Phase2FlowValues Random(string version = "0.2") => new(
        "  こんにちは😀\n\"引用\"\\path-" + GeneratedValues.Id("text") + "  ", GeneratedValues.Id("line-user"),
        GeneratedValues.Id("webhook-event"), GeneratedValues.Id("reply-token"), GeneratedValues.Id("line-message"),
        GeneratedValues.Id("line-destination"), version == "0.2" ? GeneratedValues.Id("connection") : null,
        GeneratedValues.Id("actor"), GeneratedValues.Id("character"), Guid.NewGuid().ToString(), Guid.NewGuid().ToString(),
        "  返信😀\n\"引用\"\\path-" + GeneratedValues.Id("text") + "  ",
        Enumerable.Range(1, 2).Select(i => "  返信😀\n\"引用\"\\path-" + GeneratedValues.Id("text") + ":" + i + "  ").ToArray());

    internal static Phase2FlowValues FromJson(string json) => GeneratedValues.Read<Phase2FlowValues>(json,
        "Text", "PersonId", "EventId", "ReplyToken", "MessageId", "Destination", "ConnectionId",
        "ActorId", "CharacterId", "InteractionId", "InputMessageId", "ReplyText", "Messages");

    // The contract specifies the readable members but leaves the construction syntax open.
    internal static object Create(Type type, params (string Name, object? Value)[] members)
    {
        var constructor = type.GetConstructors().SingleOrDefault(candidate =>
            candidate.GetParameters().Length == members.Length && candidate.GetParameters().All(parameter =>
                members.Any(member => string.Equals(member.Name, parameter.Name, StringComparison.OrdinalIgnoreCase))));
        if (constructor is not null)
            return constructor.Invoke(constructor.GetParameters().Select(parameter =>
                members.Single(member => string.Equals(member.Name, parameter.Name, StringComparison.OrdinalIgnoreCase)).Value).ToArray());
        var value = Activator.CreateInstance(type)!;
        foreach (var member in members)
            type.GetProperty(member.Name, BindingFlags.Public | BindingFlags.Instance)!.SetValue(value, member.Value);
        return value;
    }

    internal static string EventId(QueueItem item) =>
        item.Webhook.GetProperty("event").GetProperty("webhookEventId").GetString()!;
}
#endif
