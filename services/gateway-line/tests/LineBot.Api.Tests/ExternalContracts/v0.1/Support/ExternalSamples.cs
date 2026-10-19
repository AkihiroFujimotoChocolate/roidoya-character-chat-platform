// SPDX-License-Identifier: MIT
using LineBot.Api.Tests.Support;

namespace LineBot.Api.Tests.ExternalContracts.V01.Support;

internal sealed record ExternalValues(string Text, string PersonId, string EventId,
    string ReplyToken, string MessageId, string Destination, string ReplyText);

internal static class ExternalSamples
{
    internal static ExternalValues Random(string? text = null) => Fixed(new(
        text ?? GeneratedValues.Text(), GeneratedValues.Id("person"), GeneratedValues.Id("event"),
        GeneratedValues.Id("reply-token"), GeneratedValues.Id("message"),
        GeneratedValues.Id("destination"), "返信😀\n\"引用\"\\path-" + GeneratedValues.Id("reply")));
    internal static ExternalValues Fixed(ExternalValues values) => values;
    internal static ExternalValues FromJson(string json) => Fixed(
        GeneratedValues.Read<ExternalValues>(json, "Text", "PersonId", "EventId",
            "ReplyToken", "MessageId", "Destination", "ReplyText"));
}
