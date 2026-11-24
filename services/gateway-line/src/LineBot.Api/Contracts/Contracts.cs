// SPDX-License-Identifier: MIT
using LineBot.Api.Models;
using LineBot.Api.Processing;
using LineBot.Api.Services;

namespace LineBot.Api.Contracts;

public enum PreparationOutcome { Ready, Failed }

public sealed record InputPreparationResult<TRequest>(
    PreparationOutcome Outcome,
    TRequest? ChatRequest,
    LineReplyContext? ReplyContext,
    string? ErrorMessage)
    where TRequest : class, IChatServiceRequest;

public sealed record ContractProcessingResult(
    PreparationOutcome Outcome,
    ChatServiceResult? ChatResult,
    LineReplyContext? ReplyContext,
    string? ErrorMessage);

public interface IInputAssembler<TRequest>
    where TRequest : class, IChatServiceRequest
{
    Task<InputPreparationResult<TRequest>> PrepareAsync(
        QueueItem item, CancellationToken cancellationToken = default);
}

public interface IContractServices
{
    Task<ContractProcessingResult> ProcessAsync(
        QueueItem item, CancellationToken cancellationToken = default);
}

public interface IContractServicesResolver
{
    Task<IContractServices> ResolveAsync(
        QueueItem item, CancellationToken cancellationToken = default);
}

public sealed class ContractServices<TRequest> : IContractServices
    where TRequest : class, IChatServiceRequest
{
    private readonly IInputAssembler<TRequest> _assembler;
    private readonly IChatService<TRequest> _chat;

    public ContractServices(
        [Microsoft.Extensions.DependencyInjection.FromKeyedServices] IInputAssembler<TRequest> assembler,
        [Microsoft.Extensions.DependencyInjection.FromKeyedServices] IChatService<TRequest> chat)
    {
        _assembler = assembler;
        _chat = chat;
    }

    public async Task<ContractProcessingResult> ProcessAsync(QueueItem item, CancellationToken cancellationToken = default)
    {
        var prepared = await _assembler.PrepareAsync(item, cancellationToken);
        if (prepared.Outcome == PreparationOutcome.Failed)
            return new(prepared.Outcome, null, null, prepared.ErrorMessage);

        var result = await _chat.GenerateReplyAsync(prepared.ChatRequest!, cancellationToken);
        return new(PreparationOutcome.Ready, result, prepared.ReplyContext, null);
    }
}

public sealed class ContractServiceResolver : IContractServicesResolver
{
    private readonly string _version;
    private readonly IServiceProvider _serviceProvider;

    public ContractServiceResolver(string version, IServiceProvider serviceProvider)
    {
        _version = version;
        _serviceProvider = serviceProvider;
    }

    public Task<IContractServices> ResolveAsync(QueueItem item, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_serviceProvider.GetRequiredKeyedService<IContractServices>(_version));
    }
}
