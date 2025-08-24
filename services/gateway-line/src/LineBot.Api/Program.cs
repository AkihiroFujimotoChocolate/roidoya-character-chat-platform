// SPDX-License-Identifier: MIT
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http.Extensions;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Health probe
app.MapGet("/", () => Results.Ok("LINE Webhook Gateway - verify-only"));

// POST /line/webhook — verifies X-Line-Signature and returns 200 on success
app.MapPost("/line/webhook", async (HttpRequest req, IConfiguration cfg, ILoggerFactory lf) =>
{
    var log = lf.CreateLogger("Webhook");
    var channelSecret = cfg["Line:ChannelSecret"];

    if (string.IsNullOrWhiteSpace(channelSecret))
        return Results.Problem("Channel secret not configured", statusCode: 500);

    // Read raw body bytes
    using var ms = new MemoryStream();
    await req.Body.CopyToAsync(ms);
    var bodyBytes = ms.ToArray();

    // Get signature header
    if (!req.Headers.TryGetValue("X-Line-Signature", out var sigHeader) ||
        string.IsNullOrWhiteSpace(sigHeader))
    {
        log.LogWarning("Missing X-Line-Signature for {Method} {Url}", req.Method, req.GetDisplayUrl());
        return Results.BadRequest("missing signature");
    }

    // Compute HMAC-SHA256(rawBody) with channel secret
    var secretBytes = Encoding.UTF8.GetBytes(channelSecret);
    using var hmac = new HMACSHA256(secretBytes);
    var computed = hmac.ComputeHash(bodyBytes);

    // Compare to header (base64)
    byte[] headerBytes;
    try { headerBytes = Convert.FromBase64String(sigHeader!); }
    catch { return Results.BadRequest("invalid signature encoding"); }

    var ok = CryptographicOperations.FixedTimeEquals(headerBytes, computed);
    if (!ok)
    {
        log.LogWarning("Invalid signature for {Method} {Url}", req.Method, req.GetDisplayUrl());
        return Results.BadRequest("invalid signature");
    }

    log.LogInformation("Webhook verification OK (bytes={Length})", bodyBytes.Length);
    return Results.Ok();
});

app.Run();