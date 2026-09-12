// SPDX-License-Identifier: MIT
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Extensions;
using LineBot.Api.Models;
using LineBot.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddHttpClient<HttpV01ChatService>();
builder.Services.AddHttpClient<ILineReplyService, LineReplyService>();
builder.Services.AddSingleton<ILocalQueueService, LocalQueueService>();
builder.Services.AddSingleton<IUserLockService, UserLockService>();
builder.Services.AddSingleton<IIdempotencyService, IdempotencyService>();

// Register chat service based on mode
builder.Services.AddScoped<IChatService>(serviceProvider =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var chatMode = configuration.GetValue<string>("Chat:Mode", "Echo");
    
    return (chatMode ?? "Echo").ToLowerInvariant() switch
    {
        "http" => serviceProvider.GetRequiredService<HttpV01ChatService>(),
        "echo" => serviceProvider.GetRequiredService<EchoChatService>(),
        _ => throw new InvalidOperationException($"Unsupported Chat:Mode: {chatMode}")
    };
});

// Register individual chat services
builder.Services.AddScoped<EchoChatService>();
builder.Services.AddScoped<HttpV01ChatService>();
builder.Services.AddHostedService<MessageWorkerService>();

var app = builder.Build();

// Health probe
app.MapGet("/", (IConfiguration cfg) => 
{
    var chatMode = cfg.GetValue<string>("Chat:Mode", "Echo");
    var platform = cfg.GetValue<string>("Runtime:Platform", "Local");
    return Results.Ok($"LINE Webhook Gateway - {chatMode} Mode ({platform})");
});

// POST /line/webhook — enhanced implementation with Echo Mode
app.MapPost("/line/webhook", async (HttpRequest req, IConfiguration cfg, ILoggerFactory lf, 
    ILocalQueueService queueService, IUserLockService lockService, ILineReplyService replyService) =>
{
    var log = lf.CreateLogger("Webhook");
    var channelSecret = cfg["Line:ChannelSecret"];

    if (string.IsNullOrWhiteSpace(channelSecret))
        return Results.Problem("Channel secret not configured", statusCode: 500);

    // Get X-Line-Request-Id for logging
    req.Headers.TryGetValue("X-Line-Request-Id", out var requestIdHeader);
    var requestId = requestIdHeader.FirstOrDefault() ?? "unknown";

    // Read raw body bytes
    using var ms = new MemoryStream();
    await req.Body.CopyToAsync(ms);
    var bodyBytes = ms.ToArray();

    // Get signature header
    if (!req.Headers.TryGetValue("X-Line-Signature", out var sigHeader) ||
        string.IsNullOrWhiteSpace(sigHeader))
    {
        log.LogWarning("Missing X-Line-Signature for {Method} {Url}, RequestId={RequestId}", 
            req.Method, req.GetDisplayUrl(), requestId);
        return Results.BadRequest("missing signature");
    }

    // Compute HMAC-SHA256(rawBody) with channel secret
    var secretBytes = Encoding.UTF8.GetBytes(channelSecret);
    using var hmac = new HMACSHA256(secretBytes);
    var computed = hmac.ComputeHash(bodyBytes);

    // Compare to header (base64)
    byte[] headerBytes;
    try { headerBytes = Convert.FromBase64String(sigHeader!); }
    catch 
    { 
        log.LogWarning("Invalid signature encoding for {Method} {Url}, RequestId={RequestId}", 
            req.Method, req.GetDisplayUrl(), requestId);
        return Results.BadRequest("invalid signature encoding"); 
    }

    var ok = CryptographicOperations.FixedTimeEquals(headerBytes, computed);
    if (!ok)
    {
        log.LogWarning("Invalid signature for {Method} {Url}, RequestId={RequestId}", 
            req.Method, req.GetDisplayUrl(), requestId);
        return Results.BadRequest("invalid signature");
    }

    // Parse JSON body
    WebhookRequestBody? webhookRequest;
    try
    {
        var bodyJson = Encoding.UTF8.GetString(bodyBytes);
        webhookRequest = JsonSerializer.Deserialize<WebhookRequestBody>(bodyJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }
    catch (Exception ex)
    {
        log.LogWarning(ex, "Failed to parse webhook JSON for RequestId={RequestId}", requestId);
        return Results.BadRequest("invalid JSON");
    }

    if (webhookRequest?.Events == null)
    {
        log.LogWarning("No events in webhook request for RequestId={RequestId}", requestId);
        return Results.Ok(); // Return 200 OK even for empty events
    }

    log.LogInformation("Webhook verification OK, processing {EventCount} events (bytes={Length}), RequestId={RequestId}", 
        webhookRequest.Events.Count, bodyBytes.Length, requestId);

    // Process each event
    foreach (var webhookEvent in webhookRequest.Events)
    {
        try
        {
            await ProcessWebhookEvent(webhookEvent, queueService, lockService, replyService, cfg, log, requestId);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Error processing webhook event {WebhookEventId}, RequestId={RequestId}", 
                webhookEvent.WebhookEventId, requestId);
        }
    }

    return Results.Ok();
});

async Task ProcessWebhookEvent(WebhookEvent webhookEvent, ILocalQueueService queueService, 
    IUserLockService lockService, ILineReplyService replyService, IConfiguration cfg, ILogger log, string requestId)
{
    // Event filtering - only process text message events that are not in standby mode
    if (!WebhookHelper.ShouldProcessEvent(webhookEvent))
    {
        log.LogDebug("Ignoring event type={Type}, mode={Mode}, messageType={MessageType}, WebhookEventId={WebhookEventId}, RequestId={RequestId}", 
            webhookEvent.Type, webhookEvent.Mode, webhookEvent.Message?.Type, webhookEvent.WebhookEventId, requestId);
        return;
    }

    var userKey = WebhookHelper.GetUserKey(webhookEvent.Source);
    var isRedelivery = webhookEvent.DeliveryContext?.IsRedelivery ?? false;

    log.LogInformation("Processing message event from {UserKey}, WebhookEventId={WebhookEventId}, IsRedelivery={IsRedelivery}, RequestId={RequestId}", 
        userKey, webhookEvent.WebhookEventId, isRedelivery, requestId);

    // User-level locking
    if (!lockService.TryAcquireLock(userKey))
    {
        // User is locked (another message in flight)
        var enableLockedReply = cfg.GetValue<bool>("Lock:EnableLockedReply", true);
        if (enableLockedReply)
        {
            var lockedMessages = WebhookHelper.GetRandomMessages(cfg, "Lock:LockedMessages", 1);
            await replyService.SendReplyAsync(webhookEvent.ReplyToken!, lockedMessages, 
                webhookEvent.WebhookEventId, isRedelivery);
        }
        
        log.LogInformation("User {UserKey} is locked, sent locked message, WebhookEventId={WebhookEventId}, RequestId={RequestId}", 
            userKey, webhookEvent.WebhookEventId, requestId);
        return;
    }

    // Queue capacity check
    var replyQueueMaxSize = cfg.GetValue<int>("Reply:QueueMaxSize", 15);
    if (queueService.Count >= replyQueueMaxSize)
    {
        // Queue is full, send busy message
        var busyMessages = WebhookHelper.GetRandomMessages(cfg, "Reply:QueueFullMessages", 1);
        await replyService.SendReplyAsync(webhookEvent.ReplyToken!, busyMessages, 
            webhookEvent.WebhookEventId, isRedelivery);
        
        // Release the lock since we're not queuing
        lockService.ReleaseLock(userKey);
        
        log.LogInformation("Queue is full ({QueueSize}/{MaxSize}), sent busy message, WebhookEventId={WebhookEventId}, RequestId={RequestId}", 
            queueService.Count, replyQueueMaxSize, webhookEvent.WebhookEventId, requestId);
        return;
    }

    // Enqueue for processing
    var queueItem = new QueueItem
    {
        WebhookEventId = webhookEvent.WebhookEventId,
        ReplyToken = webhookEvent.ReplyToken!,
        UserKey = userKey,
        MessageText = webhookEvent.Message!.Text!,
        IsRedelivery = isRedelivery,
        QueuedAt = DateTime.UtcNow
    };

    if (queueService.TryEnqueue(queueItem))
    {
        log.LogInformation("Enqueued message for processing, WebhookEventId={WebhookEventId}, QueueSize={QueueSize}, RequestId={RequestId}", 
            webhookEvent.WebhookEventId, queueService.Count, requestId);
    }
    else
    {
        // Failed to enqueue, send busy message and release lock
        var busyMessages = WebhookHelper.GetRandomMessages(cfg, "Reply:QueueFullMessages", 1);
        await replyService.SendReplyAsync(webhookEvent.ReplyToken!, busyMessages, 
            webhookEvent.WebhookEventId, isRedelivery);
        
        lockService.ReleaseLock(userKey);
        
        log.LogWarning("Failed to enqueue message, sent busy message, WebhookEventId={WebhookEventId}, RequestId={RequestId}", 
            webhookEvent.WebhookEventId, requestId);
    }
}

app.Run();