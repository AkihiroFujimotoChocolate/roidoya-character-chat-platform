// SPDX-License-Identifier: MIT
using System.Text.Json;
using LineBot.Api.ConnectionManagement;
using LineBot.Api.Models;
using LineBot.Api.Processing;

namespace LineBot.Api.Contracts.V0_1;

public sealed class InputAssembler : IInputAssembler<ChatServiceRequest>
{
    private readonly IConnectionManagementClient _connections;

    public InputAssembler([Microsoft.Extensions.DependencyInjection.FromKeyedServices("0.1")] IConnectionManagementClient connections)
    {
        _connections = connections;
    }

    public async Task<InputPreparationResult<ChatServiceRequest>> PrepareAsync(QueueItem item, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var webhookEvent = item.Webhook.GetProperty("event");
            var connection = await _connections.GetConnectionAsync(null, cancellationToken);
            if (connection is null)
                return Failed("Connection lookup did not return usable LINE credentials.");

            var text = webhookEvent.GetProperty("message").GetProperty("text").GetString();
            var userId = webhookEvent.GetProperty("source").GetProperty("userId").GetString();
            var replyToken = webhookEvent.GetProperty("replyToken").GetString();
            var eventId = webhookEvent.TryGetProperty("webhookEventId", out var id) ? id.GetString() : null;
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(userId) ||
                string.IsNullOrWhiteSpace(replyToken) || string.IsNullOrWhiteSpace(eventId))
                return Failed("Webhook event is missing values required for Chat or LINE reply.");

            var authorId = "user-" + userId;
            var request = new ChatServiceRequest
            {
                RequestId = Guid.NewGuid().ToString(),
                MessageText = text,
                ConversationId = authorId,
                AuthorUserId = authorId,
                TimeoutSeconds = 20,
                MaxCharsPerMessage = 1000
            };
            var context = new LineReplyContext(new LineReplyToken(replyToken), connection.AccessToken, eventId);
            return new(PreparationOutcome.Ready, request, context, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return Failed($"Could not assemble c0.1 Chat input: {ex.Message}");
        }
    }

    private static InputPreparationResult<ChatServiceRequest> Failed(string message) =>
        new(PreparationOutcome.Failed, null, null, message);
}
