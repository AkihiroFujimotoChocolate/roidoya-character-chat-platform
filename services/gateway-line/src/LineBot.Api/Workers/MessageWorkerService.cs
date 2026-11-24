// SPDX-License-Identifier: MIT
using LineBot.Api.Contracts;
using LineBot.Api.Models;
using LineBot.Api.Processing;
using LineBot.Api.Queueing;
using LineBot.Api.Services;

namespace LineBot.Api.Workers;

public sealed class MessageWorkerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILineWebhookEventQueue _queue;
    private readonly IIdempotencyService _idempotency;
    private readonly ILogger<MessageWorkerService> _logger;

    public MessageWorkerService(
        IServiceScopeFactory scopeFactory,
        ILineWebhookEventQueue queue,
        IIdempotencyService idempotency,
        ILogger<MessageWorkerService> logger)
    {
        _scopeFactory = scopeFactory;
        _queue = queue;
        _idempotency = idempotency;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await _queue.RunAsync(ProcessItemAsync, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task ProcessItemAsync(QueueItem item, CancellationToken cancellationToken)
    {
        var eventId = string.Empty;
        try
        {
            var webhookEvent = item.Webhook.GetProperty("event");
            eventId = LineEventFilter.GetString(webhookEvent, "webhookEventId") ?? string.Empty;
            var isRedelivery = webhookEvent.TryGetProperty("deliveryContext", out var delivery) &&
                               delivery.ValueKind == System.Text.Json.JsonValueKind.Object &&
                               delivery.TryGetProperty("isRedelivery", out var redelivery) &&
                               redelivery.ValueKind == System.Text.Json.JsonValueKind.True;
            if (isRedelivery && _idempotency.IsProcessed(eventId))
            {
                _logger.LogInformation("Skipping already processed redelivery WebhookEventId={WebhookEventId}", eventId);
                return;
            }

            await using var scope = _scopeFactory.CreateAsyncScope();
            var resolver = scope.ServiceProvider.GetRequiredService<IContractServicesResolver>();
            var contract = await resolver.ResolveAsync(item, cancellationToken);
            var result = await contract.ProcessAsync(item, cancellationToken);
            if (result.Outcome == PreparationOutcome.Failed)
            {
                _logger.LogWarning("Input preparation failed for WebhookEventId={WebhookEventId}: {Error}",
                    eventId, result.ErrorMessage);
                return;
            }

            var chatResult = result.ChatResult!;
            var replyContext = result.ReplyContext!;
            if (chatResult.Messages.Count == 0)
            {
                _logger.LogWarning("Chat returned no messages for WebhookEventId={WebhookEventId}, Status={Status}",
                    eventId, chatResult.Status);
                return;
            }

            var replyService = scope.ServiceProvider.GetRequiredService<ILineReplyService>();
            if (await replyService.SendReplyAsync(replyContext, chatResult.Messages, cancellationToken))
            {
                _idempotency.MarkAsProcessed(eventId);
                _logger.LogInformation("Successfully processed WebhookEventId={WebhookEventId}", eventId);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing WebhookEventId={WebhookEventId}", eventId);
        }
    }
}
