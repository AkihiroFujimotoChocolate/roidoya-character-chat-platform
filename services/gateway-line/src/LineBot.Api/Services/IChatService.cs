// SPDX-License-Identifier: MIT
using LineBot.Api.Models;

namespace LineBot.Api.Services;

public interface IChatService
{
    Task<ChatServiceResult> GenerateReplyAsync(ChatServiceRequest request, CancellationToken cancellationToken = default);
}
