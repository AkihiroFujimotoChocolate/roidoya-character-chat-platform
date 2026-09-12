// SPDX-License-Identifier: MIT
using LineBot.Api.Models;
using LineBot.Api.Services;

namespace LineBot.Api.Tests;

public class EchoChatServiceTests
{
    [Fact]
    public async Task GenerateReplyAsync_ReturnsFallbackForEmptyInput()
    {
        var configuration = TestSupport.BuildConfiguration(new Dictionary<string, string?>
        {
            ["Chat:Fallbacks:Default"] = "fallback"
        });
        var service = new EchoChatService(configuration, TestSupport.Logger<EchoChatService>());

        var result = await service.GenerateReplyAsync(new ChatServiceRequest
        {
            MessageText = "",
            MaxCharsPerMessage = 1000
        });

        Assert.Equal("ok", result.Status);
        Assert.True(result.FallbackUsed);
        Assert.Equal(new[] { "fallback" }, result.Messages);
    }

    [Fact]
    public async Task GenerateReplyAsync_SplitsByMaxCharsPerMessage()
    {
        var configuration = TestSupport.BuildConfiguration(new Dictionary<string, string?>());
        var service = new EchoChatService(configuration, TestSupport.Logger<EchoChatService>());

        var result = await service.GenerateReplyAsync(new ChatServiceRequest
        {
            MessageText = "1234567890",
            MaxCharsPerMessage = 4
        });

        Assert.Equal("ok", result.Status);
        Assert.False(result.FallbackUsed);
        Assert.Equal(new[] { "1234", "5678", "90" }, result.Messages);
    }
}
