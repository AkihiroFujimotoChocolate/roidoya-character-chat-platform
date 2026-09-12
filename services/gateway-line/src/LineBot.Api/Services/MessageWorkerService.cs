// SPDX-License-Identifier: MIT
using LineBot.Api.Models;
using LineBot.Api.Services;

namespace LineBot.Api.Services;

public class MessageWorkerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MessageWorkerService> _logger;
    private readonly double _qps;
    private readonly TimeSpan _delayBetweenRequests;
    private readonly int _chatRequestTimeoutSeconds;
    private readonly int _chatMaxCharsPerMessage;

    public MessageWorkerService(IServiceProvider serviceProvider, ILogger<MessageWorkerService> logger, IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _qps = configuration.GetValue<double>("Reply:Qps", 5.0);
        _delayBetweenRequests = TimeSpan.FromMilliseconds(1000.0 / _qps);
        _chatRequestTimeoutSeconds = configuration.GetValue<int>("Chat:RequestTimeoutSeconds", 20);
        _chatMaxCharsPerMessage = configuration.GetValue<int>("Chat:MaxCharsPerMessage", 1000);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Message worker service started with QPS limit: {Qps}", _qps);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var queueService = scope.ServiceProvider.GetRequiredService<ILocalQueueService>();
                var lockService = scope.ServiceProvider.GetRequiredService<IUserLockService>();
                var chatService = scope.ServiceProvider.GetRequiredService<IChatService>();
                var replyService = scope.ServiceProvider.GetRequiredService<ILineReplyService>();
                var idempotencyService = scope.ServiceProvider.GetRequiredService<IIdempotencyService>();

                var queueItem = queueService.TryDequeue();
                if (queueItem == null)
                {
                    // No items in queue, wait a bit
                    await Task.Delay(100, stoppingToken);
                    continue;
                }

                _logger.LogInformation("Processing message for WebhookEventId={WebhookEventId}, UserKey={UserKey}, IsRedelivery={IsRedelivery}", 
                    queueItem.WebhookEventId, queueItem.UserKey, queueItem.IsRedelivery);

                try
                {
                    await ProcessQueueItem(queueItem, chatService, replyService, idempotencyService, stoppingToken);
                }
                finally
                {
                    // Always release the lock after processing
                    lockService.ReleaseLock(queueItem.UserKey);
                }

                // Apply QPS throttling
                await Task.Delay(_delayBetweenRequests, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in message worker service");
                await Task.Delay(1000, stoppingToken); // Wait before retrying
            }
        }

        _logger.LogInformation("Message worker service stopped");
    }

    private async Task ProcessQueueItem(QueueItem queueItem, IChatService chatService, ILineReplyService replyService, IIdempotencyService idempotencyService, CancellationToken cancellationToken)
    {
        // Check for redelivery idempotency
        if (queueItem.IsRedelivery && idempotencyService.IsProcessed(queueItem.WebhookEventId))
        {
            _logger.LogInformation("Skipping redelivered message that was already processed: WebhookEventId={WebhookEventId}", 
                queueItem.WebhookEventId);
            return;
        }

        try
        {
            // Generate chat response
            var chatRequest = new ChatServiceRequest
            {
                RequestId = Guid.NewGuid().ToString(),
                MessageText = queueItem.MessageText,
                TimeoutSeconds = _chatRequestTimeoutSeconds,
                MaxCharsPerMessage = _chatMaxCharsPerMessage,
                ConversationId = queueItem.UserKey,
                AuthorUserId = queueItem.UserKey
            };

            var chatResponse = await chatService.GenerateReplyAsync(chatRequest, cancellationToken);

            if (chatResponse.Messages.Count > 0)
            {
                // Send reply via LINE API
                var success = await replyService.SendReplyAsync(
                    queueItem.ReplyToken, 
                    chatResponse.Messages, 
                    queueItem.WebhookEventId, 
                    queueItem.IsRedelivery, 
                    cancellationToken);

                if (success)
                {
                    // Mark as processed for idempotency
                    idempotencyService.MarkAsProcessed(queueItem.WebhookEventId);
                    _logger.LogInformation("Successfully processed message for WebhookEventId={WebhookEventId}", 
                        queueItem.WebhookEventId);
                }
                else
                {
                    _logger.LogWarning("Failed to send reply for WebhookEventId={WebhookEventId}", 
                        queueItem.WebhookEventId);
                }
            }
            else
            {
                _logger.LogWarning("Chat service returned no messages for WebhookEventId={WebhookEventId}, Status={Status}", 
                    queueItem.WebhookEventId, chatResponse.Status);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing queue item for WebhookEventId={WebhookEventId}", 
                queueItem.WebhookEventId);
        }
    }
}