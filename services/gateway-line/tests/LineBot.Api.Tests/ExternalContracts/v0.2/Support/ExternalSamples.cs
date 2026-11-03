// SPDX-License-Identifier: MIT
using LineBot.Api.Tests.Support;
using LineBot.Api.Tests.ExternalContracts.V01.Line;
namespace LineBot.Api.Tests.ExternalContracts.V02.Support;

internal sealed record ExternalValues(string Text, string PersonId, string EventId,
    string ReplyToken, string MessageId, string Destination, string ReplyText,
    string CharacterId, string OutputMessageId);

internal static class ExternalSamples
{
    internal static ExternalValues Random(string? text = null) => Fixed(new(
        text ?? ("  こんにちは😀\n\"引用\"\\path-" + GeneratedValues.Id("text") + "  "),
        GeneratedValues.Id("line-user"), GeneratedValues.Id("webhook-event"),
        GeneratedValues.Id("reply-token"), GeneratedValues.Id("line-message"),
        GeneratedValues.Id("line-destination"),
        "  返信😀\n\"引用\"\\path-" + GeneratedValues.Id("text") + "  ",
        GeneratedValues.Id("character"), GeneratedValues.Id("output-message")));
    internal static ExternalValues Fixed(ExternalValues values) => values;
    internal static ExternalValues FromJson(string json) => Fixed(
        GeneratedValues.Read<ExternalValues>(json, "Text", "PersonId", "EventId",
            "ReplyToken", "MessageId", "Destination", "ReplyText", "CharacterId", "OutputMessageId"));
    internal static byte[] Body(this ExternalValues data, bool redelivery = false,
        string mode = "active", string kind = "message", string messageKind = "text") =>
        LineWebhookSamples.Webhook(redelivery, mode, kind, messageKind, data.Text, data.EventId,
            data.PersonId, data.ReplyToken, data.MessageId, data.Destination);
}
