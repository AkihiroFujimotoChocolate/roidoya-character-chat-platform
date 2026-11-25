// SPDX-License-Identifier: MIT
using LineBot.Api.Chat;

namespace LineBot.Api.Chat;

public interface IChatService<TRequest>
    where TRequest : class, IChatServiceRequest
{
    Task<ChatServiceResult> GenerateReplyAsync(
        TRequest request, CancellationToken cancellationToken = default);
}
