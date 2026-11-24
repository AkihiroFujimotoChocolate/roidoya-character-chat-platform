// SPDX-License-Identifier: MIT
using System.Text.Json;
using LineBot.Api.ConnectionManagement;
using LineBot.Api.Models;
using LineBot.Api.Processing;

namespace LineBot.Api.Contracts.V0_2;

public sealed class InputAssembler : IInputAssembler<ChatServiceRequest>
{
    private readonly IConnectionManagementClient _connections;

    public InputAssembler([Microsoft.Extensions.DependencyInjection.FromKeyedServices("0.2")] IConnectionManagementClient connections)
    {
        _connections = connections;
    }

    public async Task<InputPreparationResult<ChatServiceRequest>> PrepareAsync(QueueItem item, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var webhookEvent = item.Webhook.GetProperty("event");
            var connection = await _connections.GetConnectionAsync(item.ConnectionId, cancellationToken);
            if (connection is null)
                return Failed("Connection lookup did not return usable LINE credentials or character data.");
            var userId = webhookEvent.GetProperty("source").GetProperty("userId").GetString();
            var text = webhookEvent.GetProperty("message").GetProperty("text").GetString();
            var replyToken = webhookEvent.GetProperty("replyToken").GetString();
            var eventId = webhookEvent.TryGetProperty("webhookEventId", out var id) ? id.GetString() : null;
            var destination = item.Webhook.TryGetProperty("destination", out var rootDestination)
                ? rootDestination.GetString()
                : null;
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(text) ||
                string.IsNullOrWhiteSpace(replyToken) || string.IsNullOrWhiteSpace(eventId) ||
                string.IsNullOrWhiteSpace(destination))
                return Failed("Webhook event is missing values required for Chat or LINE reply.");

            var actorId = await _connections.ResolveActorAsync(userId, cancellationToken);
            if (string.IsNullOrWhiteSpace(actorId))
                return Failed("Actor lookup did not return a usable actor ID.");

            var request = new ChatServiceRequest
            {
                InteractionId = Guid.NewGuid().ToString(),
                ChannelType = "line",
                ChannelInstanceId = destination,
                ActorId = actorId,
                CharacterId = connection.CharacterId,
                InputMessageId = Guid.NewGuid().ToString(),
                MessageText = text
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
            return Failed($"Could not assemble c0.2 Chat input: {ex.Message}");
        }
    }

    private static InputPreparationResult<ChatServiceRequest> Failed(string message) =>
        new(PreparationOutcome.Failed, null, null, message);
}
