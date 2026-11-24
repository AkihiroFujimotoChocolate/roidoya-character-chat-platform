// SPDX-License-Identifier: MIT
using System.Text.Json;
using LineBot.Api.Chat;
using LineBot.Api.ConnectionManagement;
using LineBot.Api.Processing;
using LineBot.Api.Queueing;

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
            LineConnection? connection;
            try
            {
                connection = await _connections.GetConnectionAsync(null, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (HttpRequestException ex)
            {
                return Failed($"Connection lookup failed: {ex.Message}");
            }
            catch (JsonException ex)
            {
                return Failed($"Connection lookup returned invalid JSON: {ex.Message}");
            }
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
    }

    private static InputPreparationResult<ChatServiceRequest> Failed(string message) =>
        new(PreparationOutcome.Failed, null, null, message);
}
