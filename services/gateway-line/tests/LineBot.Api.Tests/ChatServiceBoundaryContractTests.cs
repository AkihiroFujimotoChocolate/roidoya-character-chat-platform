// SPDX-License-Identifier: MIT
using LineBot.Api.Models;
using LineBot.Api.Services;

namespace LineBot.Api.Tests;

public class ChatServiceBoundaryContractTests
{
    [Fact]
    public void ChatServiceRequest_ExposesOnlyWorkerFacingSemantics()
    {
        var propertyNames = typeof(ChatServiceRequest).GetProperties().Select(p => p.Name).OrderBy(x => x).ToArray();

        Assert.Equal(new[]
        {
            "AuthorUserId",
            "ConversationId",
            "MaxCharsPerMessage",
            "MessageText",
            "RequestId",
            "TimeoutSeconds"
        }, propertyNames);
    }

    [Fact]
    public void ChatServiceResult_ExposesOnlyWorkerFacingSemantics()
    {
        var propertyNames = typeof(ChatServiceResult).GetProperties().Select(p => p.Name).OrderBy(x => x).ToArray();

        Assert.Equal(new[]
        {
            "Messages",
            "Status"
        }, propertyNames);
    }

    [Fact]
    public void IChatService_UsesSemanticBoundaryTypes()
    {
        var method = typeof(IChatService).GetMethod(nameof(IChatService.GenerateReplyAsync));
        Assert.NotNull(method);

        var parameters = method!.GetParameters();
        Assert.Equal(typeof(ChatServiceRequest), parameters[0].ParameterType);

        var taskType = method.ReturnType;
        Assert.Equal(typeof(Task<ChatServiceResult>), taskType);
    }
}
