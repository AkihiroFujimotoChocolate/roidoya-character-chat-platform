// SPDX-License-Identifier: MIT
using LineBot.Api.Models;

namespace LineBot.Api.Services;

public interface IChatService<TRequest>
    where TRequest : class, IChatServiceRequest
{
    Task<ChatServiceResult> GenerateReplyAsync(
        TRequest request, CancellationToken cancellationToken = default);
}
