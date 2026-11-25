#if PHASE2_GATEWAY_TESTS
// SPDX-License-Identifier: MIT
using LineBot.Api.Chat;
using LineBot.Api.Tests.ExternalContracts.V01.Chat;
using Request = LineBot.Api.Contracts.V0_1.ChatServiceRequest;
using LineBot.Api.Processing;
using LineBot.Api.Tests.ExternalContracts.V01.Line;
using LineBot.Api.Tests.ExternalContracts.V01.Support;
using LineBot.Api.Tests.InternalContracts.Support;
using LineBot.Api.Tests.InternalContracts.Support.V01;
using LineBot.Api.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace LineBot.Api.Tests.InternalContracts.Worker;

public sealed class Phase2ReplyBoundaryTestsC01(ITestOutputHelper output) : DiagnosticTest(output)
{
    [Fact(DisplayName = "C01-Internal-11-01: Successful LINE delivery returns true independently of Chat status")]
    [Trait("Case", "C01-Internal-11")]
    public async Task Reply_Http200_ReturnsTrue()
    {
        var data = Phase2FlowSamples.Random("0.1");
        var input = data.ChatV01();
        var outputId = GeneratedValues.Id("output-message");
        using var chat = new CaptureHandler((_, _) =>
            Task.FromResult(ChatV01Samples.Ok(data.ReplyText)));
        var context = await data.PrepareReplyContextAsync();
        using var line = new CaptureHandler((_, _) => Task.FromResult(LineReplySamples.SuccessResponse()));
        using var other = new CaptureHandler((_, _) => throw new InvalidOperationException("Unexpected outbound request."));
        using var host = new GatewayApplicationFactory(chat, line, other);
        Trace("C01-Internal-11-01", new { data, input, context, host.Settings, ChatStatus = 200, ChatOutputId = outputId,
            LineStatus = 200, LineBody = LineReplySamples.SuccessResponseJson, Expected = true, WaitSeconds = 10 });
        using var scope = host.Services.CreateScope();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            var adapter = scope.ServiceProvider.GetRequiredKeyedService<IChatService<Request>>("0.1");
            var result = await adapter.GenerateReplyAsync(input, deadline.Token).WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(new[] { data.ReplyText }, result.Messages);
            var reply = scope.ServiceProvider.GetRequiredService<ILineReplyService>();
            Assert.True(await reply.SendReplyAsync(context, result.Messages, deadline.Token).WaitAsync(TimeSpan.FromSeconds(10)));
            var sent = await line.NextAsync();
            Assert.Equal("https://api.line.me/v2/bot/message/reply", sent.Uri.AbsoluteUri);
            await line.AssertNoPendingRequestsAsync();
            await host.StopHostAsync();
            Assert.Single(chat.Requests);
            Assert.Single(line.Requests);
            Assert.Empty(other.Requests);
            Assert.Empty(host.Unexpected.Requests);
        }
        finally { await host.StopHostAsync(); }
    }
}
#endif
