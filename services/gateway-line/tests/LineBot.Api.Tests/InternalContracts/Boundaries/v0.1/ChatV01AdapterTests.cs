#if PHASE2_GATEWAY_TESTS
// SPDX-License-Identifier: MIT
using LineBot.Api.Chat;
using LineBot.Api.Processing;
using LineBot.Api.Tests.ExternalContracts.V01.Chat;
using LineBot.Api.Tests.ExternalContracts.V01.Support;
using LineBot.Api.Tests.InternalContracts.Support;
using LineBot.Api.Tests.InternalContracts.Support.V01;
using LineBot.Api.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;
using Request = LineBot.Api.Contracts.V0_1.ChatServiceRequest;

namespace LineBot.Api.Tests.InternalContracts.Boundaries.V01;

public sealed class ChatV01AdapterTests(ITestOutputHelper output) : DiagnosticTest(output)
{
    [Theory(DisplayName = "C01-Internal-03: Supplied request values reach HTTP and replies are selected")]
    [InlineData("C01-Internal-03-01", "Normal text", false)]
    [InlineData("C01-Internal-03-02", "Provider error selects default fallback", true)]
    [Trait("Case", "C01-Internal-03")]
    public async Task Chat_SuppliedIdsAndNormalResponse_PreserveBoundaryValues(string caseId, string description, bool providerError)
    {
        var data = Phase2FlowSamples.Random("0.1");
        var outputId = GeneratedValues.Id("output-message");
        var text = providerError ? ChatV01Samples.DefaultFallback : data.ReplyText;
        var request = data.ChatV01();
        using var httpResponse = providerError ? HttpResponses.Json("{\"status\":\"provider_error\",\"messages\":[],\"error\":{\"code\":\"provider-test\",\"message\":\"diagnostic-only\"}}") : ChatV01Samples.Ok(text);
        var responseBody = await httpResponse.Content.ReadAsStringAsync();
        using var chat = new CaptureHandler((_, _) => Task.FromResult(providerError ? HttpResponses.Json("{\"status\":\"provider_error\",\"messages\":[],\"error\":{\"code\":\"provider-test\",\"message\":\"diagnostic-only\"}}") : ChatV01Samples.Ok(text)));
        using var other = new CaptureHandler((_, _) => throw new InvalidOperationException("Unexpected outbound request."));
        using var host = new GatewayApplicationFactory(chat, other, other);
        Trace(caseId, new { description, host.Settings, request, ResponseBody = responseBody,
            ResponseStatus = 200, ExpectedRequestId = data.InteractionId,
            ExpectedMessages = new[] { text }, WaitLimitSeconds = 10 });
        using var scope = host.Services.CreateScope();
        using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            var result = await scope.ServiceProvider.GetRequiredKeyedService<IChatService<Request>>("0.1")
                .GenerateReplyAsync(request, limit.Token).WaitAsync(TimeSpan.FromSeconds(10));
            var sent = await chat.NextAsync();
            using var json = sent.Json();
            Assert.Equal(data.InteractionId, json.RootElement.GetProperty("request_id").GetString());
            Assert.Equal(data.Text, json.RootElement.GetProperty("message").GetProperty("text").GetString());
            Assert.Equal(new[] { text }, result.Messages);
            await chat.AssertNoPendingRequestsAsync();
            await host.StopHostAsync();
            Assert.Empty(other.Requests);
            Assert.Empty(host.Unexpected.Requests);
            Assert.Single(chat.Requests);
            Trace(caseId + "-observed", new { Request = sent, ResponseBody = responseBody, Result = result });
        }
        finally { await host.StopHostAsync(); }
    }
}

#endif
