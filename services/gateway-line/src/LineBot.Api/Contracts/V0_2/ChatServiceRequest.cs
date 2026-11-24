// SPDX-License-Identifier: MIT
using LineBot.Api.Models;

namespace LineBot.Api.Contracts.V0_2;

public class ChatServiceRequest : IChatServiceRequest
{
    public string InteractionId { get; set; } = string.Empty;
    public string ChannelType { get; set; } = string.Empty;
    public string ChannelInstanceId { get; set; } = string.Empty;
    public string ActorId { get; set; } = string.Empty;
    public string CharacterId { get; set; } = string.Empty;
    public string InputMessageId { get; set; } = string.Empty;
    public string MessageText { get; set; } = string.Empty;
}
