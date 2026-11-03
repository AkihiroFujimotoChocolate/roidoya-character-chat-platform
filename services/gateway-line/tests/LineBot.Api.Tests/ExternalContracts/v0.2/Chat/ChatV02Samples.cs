// SPDX-License-Identifier: MIT
using LineBot.Api.Tests.Support;
using System.Text.Json;
namespace LineBot.Api.Tests.ExternalContracts.V02.Chat;
internal static class ChatV02Samples
{
    internal const string DefaultFallback = "The service is temporarily unavailable.";
    internal const string TimeoutFallback = "The request timed out. Please try again.";
    internal static HttpResponseMessage Ok(string interactionId, string outputId, string text) =>
        HttpResponses.Json(JsonSerializer.Serialize(new {
            interaction_id = interactionId, status = "ok",
            messages = new[] { new { id = outputId, content = new[] { new { type = "text", text } } } }
        }));
    internal static HttpResponseMessage Ok(CapturedHttp request, string outputId, string text)
    {
        using var json = request.Json();
        return Ok(json.RootElement.GetProperty("interaction_id").GetString()!, outputId, text);
    }
}
