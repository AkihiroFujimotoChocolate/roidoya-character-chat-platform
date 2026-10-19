// SPDX-License-Identifier: MIT
using LineBot.Api.Tests.Support;

namespace LineBot.Api.Tests.ExternalContracts.V01.Line;

internal static class LineReplySamples
{
    internal const string AccessToken = "access-token-test";
    internal const string SuccessResponseJson = "{\"sentMessages\":[{\"id\":\"sent-test\",\"quoteToken\":\"quote-test\"}]}";
    internal static HttpResponseMessage SuccessResponse() => HttpResponses.Json(SuccessResponseJson);
}
