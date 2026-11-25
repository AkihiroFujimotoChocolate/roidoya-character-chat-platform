// SPDX-License-Identifier: MIT
using System.Text.Json;

namespace LineBot.Api.Queueing;

public class QueueItem
{
    public JsonElement Webhook { get; set; }
    public string? ConnectionId { get; set; }
}
