// SPDX-License-Identifier: MIT
using System.Text.Json;

namespace LineBot.Api.Tests.Support;

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
