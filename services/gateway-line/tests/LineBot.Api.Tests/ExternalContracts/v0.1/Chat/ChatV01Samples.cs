// SPDX-License-Identifier: MIT
using System.Text.Json;
using LineBot.Api.Tests.Support;

namespace LineBot.Api.Tests.ExternalContracts.V01.Chat;

internal static class ChatV01Samples
{
    internal const string DefaultFallback = "The service is temporarily unavailable.";
    internal const string TimeoutFallback = "The request timed out. Please try again.";
    internal static HttpResponseMessage Ok(params string[] messages) =>
        HttpResponses.Json(JsonSerializer.Serialize(new { request_id = "correlation-test", status = "ok", messages, fallback_used = false }));
}
