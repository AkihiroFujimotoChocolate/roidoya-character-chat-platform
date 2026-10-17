// SPDX-License-Identifier: MIT
using System.Security.Cryptography;
using System.Text.Json;
using LineBot.Api.Models;
using LineBot.Api.Services;
namespace LineBot.Api.Tests.Support;

internal sealed record RequestValues(string? RequestId, string ConversationId, string AuthorUserId,
    string MessageText, int TimeoutSeconds, int MaxCharsPerMessage);

internal sealed record RequestExpected(string? RequestId, string ConversationId, string AuthorUserId,
    string MessageText, int TimeoutSeconds, int MaxCharsPerMessage);

internal sealed record RequestSample(RequestValues Values, RequestExpected Expected)
{
    internal ChatServiceRequest Input => new() {
        RequestId = Values.RequestId, ConversationId = Values.ConversationId,
        AuthorUserId = Values.AuthorUserId, MessageText = Values.MessageText,
        TimeoutSeconds = Values.TimeoutSeconds, MaxCharsPerMessage = Values.MaxCharsPerMessage
    };
}

internal static class RequestSamples
{
    internal static RequestSample CreateRandom(string? text = null) => Create(NewValues(text));
    internal static RequestSample CreateWithoutRequestId(string? text = null) =>
        Create(NewValues(text) with { RequestId = null });

    // Replay takes every value explicitly; it never fills omitted fields randomly.
    internal static RequestSample Create(RequestValues values) => new(values,
        new(values.RequestId, values.ConversationId, "line-" + values.AuthorUserId,
            values.MessageText, values.TimeoutSeconds, values.MaxCharsPerMessage));

    internal static RequestSample FromJson(string valuesJson) =>
        Create(GeneratedValues.Read<RequestValues>(valuesJson,
            "RequestId", "ConversationId", "AuthorUserId", "MessageText", "TimeoutSeconds", "MaxCharsPerMessage"));

    private static RequestValues NewValues(string? text) => new(
        GeneratedValues.Id("request"), GeneratedValues.Id("conversation"), GeneratedValues.Id("author"),
        text ?? GeneratedValues.Text(), RandomNumberGenerator.GetInt32(2, 6), RandomNumberGenerator.GetInt32(80, 122));
}

internal sealed record FlowValues(string Text, string ConversationId, string PersonId, string EventId,
    string ReplyToken, string MessageId, string Destination, string[] Messages, string DefaultFallback)
{
    internal QueueItem Job(bool redelivery = false) => new() {
        MessageText = Text, UserKey = ConversationId, WebhookEventId = EventId,
        ReplyToken = ReplyToken, IsRedelivery = redelivery
    };
    internal byte[] Webhook(bool redelivery = false, string mode = "active",
        string kind = "message", string messageKind = "text") =>
        Samples.Webhook(redelivery, mode, kind, messageKind, Text, EventId, PersonId,
            ReplyToken, MessageId, Destination);
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
                (richMessages ? Samples.RichText : "reply") + ":" + i + ":" + GeneratedValues.Id("text")).ToArray(),
            GeneratedValues.Id("fallback")));
    }
    internal static FlowValues Create(FlowValues values) => values with { Messages = values.Messages.ToArray() };
    internal static FlowValues FromJson(string valuesJson) =>
        Create(GeneratedValues.Read<FlowValues>(valuesJson, "Text", "ConversationId", "PersonId",
            "EventId", "ReplyToken", "MessageId", "Destination", "Messages", "DefaultFallback"));
}

internal static class GeneratedValues
{
    // ASCII suffixes keep valid IDs short; role prefixes expose swapped fields.
    internal static string Id(string role) => role + "-" + Guid.NewGuid().ToString("N");
    internal static string Text() => "本文-" + Id("text");

    internal static T Read<T>(string json, params string[] requiredFields) where T : class
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        foreach (var field in requiredFields)
            if (!root.TryGetProperty(field, out _))
                throw new ArgumentException("Replay value is missing: " + field, nameof(json));
        return JsonSerializer.Deserialize<T>(json)
            ?? throw new ArgumentException("Replay values must be an object.", nameof(json));
    }
}
