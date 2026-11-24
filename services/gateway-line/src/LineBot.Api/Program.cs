// SPDX-License-Identifier: MIT
using LineBot.Api.ConnectionManagement;
using LineBot.Api.Chat;
using LineBot.Api.Processing;
using LineBot.Api.Queueing;
using LineBot.Api.Queueing.Local;
using LineBot.Api.Services;
using LineBot.Api.Workers;

var builder = WebApplication.CreateBuilder(args);
var version = builder.Configuration["ContractVersion"] ?? "0.1";
if (version is not ("0.1" or "0.2"))
    throw new InvalidOperationException($"Unsupported ContractVersion '{version}'.");

builder.Services.AddSingleton<ILineWebhookEventQueue, LocalLineWebhookEventQueue>();
builder.Services.AddSingleton<IIdempotencyService, IdempotencyService>();
builder.Services.AddScoped<ILineEventFilter, LineEventFilter>();
builder.Services.AddScoped<IContractServicesResolver>(serviceProvider =>
    new ContractServiceResolver(version, serviceProvider));

builder.Services.AddHttpClient("0.1").AddAsKeyed();
builder.Services.AddHttpClient("0.2").AddAsKeyed();
builder.Services.AddHttpClient("connection-0.1").AddAsKeyed();
builder.Services.AddHttpClient("connection-0.2").AddAsKeyed();
builder.Services.AddKeyedScoped<IConnectionManagementClient, V01ConnectionManagementClient>("0.1");
builder.Services.AddKeyedScoped<IConnectionManagementClient, V02ConnectionManagementClient>("0.2");

builder.Services.AddKeyedScoped<IInputAssembler<LineBot.Api.Contracts.V0_1.ChatServiceRequest>,
    LineBot.Api.Contracts.V0_1.InputAssembler>("0.1");
builder.Services.AddKeyedScoped<IChatService<LineBot.Api.Contracts.V0_1.ChatServiceRequest>,
    LineBot.Api.Contracts.V0_1.HttpChatService>("0.1");
builder.Services.AddKeyedScoped<IContractServices,
    ContractServices<LineBot.Api.Contracts.V0_1.ChatServiceRequest>>("0.1");
builder.Services.AddKeyedScoped<IInputAssembler<LineBot.Api.Contracts.V0_2.ChatServiceRequest>,
    LineBot.Api.Contracts.V0_2.InputAssembler>("0.2");
builder.Services.AddKeyedScoped<IChatService<LineBot.Api.Contracts.V0_2.ChatServiceRequest>,
    LineBot.Api.Contracts.V0_2.HttpChatService>("0.2");
builder.Services.AddKeyedScoped<IContractServices,
    ContractServices<LineBot.Api.Contracts.V0_2.ChatServiceRequest>>("0.2");

builder.Services.AddHttpClient<LineReplyService>();
builder.Services.AddScoped<ILineReplyService>(serviceProvider => serviceProvider.GetRequiredService<LineReplyService>());
builder.Services.AddHostedService<MessageWorkerService>();

var app = builder.Build();

app.MapGet("/", (IConfiguration configuration) =>
    Results.Ok($"LINE Webhook Gateway ({configuration.GetValue<string>("Runtime:Platform", "Local")})"));

if (version == "0.1")
{
    app.MapPost("/line/webhook", (
        HttpRequest request,
        [Microsoft.Extensions.DependencyInjection.FromKeyedServices("0.1")] IConnectionManagementClient connections,
        ILineEventFilter filter,
        ILineWebhookEventQueue queue,
        ILineReplyService reply,
        IConfiguration configuration,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken) =>
        ReceiveWebhookAsync(request, null, version, connections, filter, queue, reply, configuration,
            loggerFactory.CreateLogger("Webhook"), cancellationToken));
}
else
{
    app.MapPost("/line/webhook/{connection_id}", (
        HttpRequest request,
        string connection_id,
        [Microsoft.Extensions.DependencyInjection.FromKeyedServices("0.2")] IConnectionManagementClient connections,
        ILineEventFilter filter,
        ILineWebhookEventQueue queue,
        ILineReplyService reply,
        IConfiguration configuration,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken) =>
        ReceiveWebhookAsync(request, connection_id, version, connections, filter, queue, reply, configuration,
            loggerFactory.CreateLogger("Webhook"), cancellationToken));
}

app.Run();

static async Task<IResult> ReceiveWebhookAsync(
    HttpRequest request,
    string? connectionId,
    string contractVersion,
    IConnectionManagementClient connections,
    ILineEventFilter filter,
    ILineWebhookEventQueue queue,
    ILineReplyService reply,
    IConfiguration configuration,
    ILogger logger,
    CancellationToken cancellationToken)
{
    var selectedConnection = await connections.GetConnectionAsync(connectionId, cancellationToken);
    if (selectedConnection is null ||
        (contractVersion == "0.2" && selectedConnection.Id != connectionId))
    {
        logger.LogWarning("LINE connection lookup failed for connection {ConnectionId}", connectionId);
        return Results.StatusCode(StatusCodes.Status502BadGateway);
    }

    using var body = new MemoryStream();
    await request.Body.CopyToAsync(body, cancellationToken);
    var bodyBytes = body.ToArray();
    request.Headers.TryGetValue("X-Line-Signature", out var signature);
    VerifiedLineWebhook? verified;
    try
    {
        if (!VerifiedLineWebhook.TryCreate(bodyBytes, signature.FirstOrDefault(),
                selectedConnection.ChannelSecret, out verified))
        {
            logger.LogWarning("Missing or invalid X-Line-Signature");
            return Results.BadRequest("missing or invalid signature");
        }
    }
    catch (System.Text.Json.JsonException ex)
    {
        logger.LogWarning(ex, "Invalid webhook JSON");
        return Results.BadRequest("invalid JSON");
    }

    IReadOnlyList<QueueItem> items;
    try
    {
        items = WebhookEventSplitter.SplitEvents(verified!, contractVersion == "0.2" ? connectionId : null);
    }
    catch (System.Text.Json.JsonException ex)
    {
        logger.LogWarning(ex, "Invalid webhook JSON");
        return Results.BadRequest("invalid JSON");
    }

    foreach (var item in items)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!await filter.IsTargetAsync(item, cancellationToken))
            continue;

        var result = await queue.TryEnqueueAsync(item, cancellationToken);
        if (result == QueueWriteResult.Accepted)
            continue;

        var fullMessages = configuration.GetSection("Reply:QueueFullMessages").Get<List<string>>();
        if (fullMessages is { Count: > 0 })
        {
            var webhookEvent = item.Webhook.GetProperty("event");
            var replyToken = webhookEvent.GetProperty("replyToken").GetString()!;
            var eventId = webhookEvent.TryGetProperty("webhookEventId", out var id) ? id.GetString() ?? string.Empty : string.Empty;
            var replyContext = new LineReplyContext(
                new LineReplyToken(replyToken),
                selectedConnection.AccessToken,
                eventId);
            await reply.SendReplyAsync(replyContext, new List<string> { fullMessages[Random.Shared.Next(fullMessages.Count)] }, cancellationToken);
        }
    }
    return Results.Ok();
}

public partial class Program { }
