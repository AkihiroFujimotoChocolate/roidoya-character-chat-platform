// SPDX-License-Identifier: MIT
using System.Net;
using System.Text;

namespace LineBot.Api.Tests.Support;

internal static class HttpResponses
{
    internal static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
}
