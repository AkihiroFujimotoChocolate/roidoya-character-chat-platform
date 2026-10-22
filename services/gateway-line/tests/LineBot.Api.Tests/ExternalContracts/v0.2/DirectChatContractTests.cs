// SPDX-License-Identifier: MIT
using System.Net;
using LineBot.Api.Tests.Support;
using LineBot.Api.Tests.ExternalContracts.V02.Support;
using Xunit.Abstractions;

namespace LineBot.Api.Tests.ExternalContracts.V02;

public sealed class DirectChatContractTests : DiagnosticTest
{
    public DirectChatContractTests(ITestOutputHelper output) : base(output) { }

    [Fact(DisplayName = "V02-External-01: A signed text produces the phase-1 c0.2 envelope and matching LINE reply")]
    [Trait("Case", "V02-External-01")]
    public async Task SignedText_PreservesEnvelopeAndReply()
    {
        await AssertRoundTripAsync("V02-External-01", DirectChatSamples.Create());
    }

    [Theory(DisplayName = "V02-External-02: Selected endpoint follows the startup configuration")]
    [Trait("Case", "V02-External-02")]
    [InlineData("V02-External-02-01", "Explicit endpoint takes precedence over the template", "endpoint", "https://chat.test/custom/direct")]
    [InlineData("V02-External-02-02", "The selected version expands the endpoint template", "template", "https://chat.test/custom/v0.2/direct")]
    [InlineData("V02-External-02-03", "Absent endpoint and template use the c0.2 default path", "default", "https://chat.test/chat/v0.2/generate-replies")]
    [InlineData("V02-External-02-04", "An absolute endpoint does not require either base URL", "absolute", "https://absolute.test/direct")]
    [InlineData("V02-External-02-05", "App BaseUrl resolves a relative path when Chat BaseUrl is empty", "app-base", "https://app.test/chat/v0.2/generate-replies")]
    public async Task ConfiguredRoute_SendsExpectedRequest(string caseId, string condition, string route, string expected)
    {
        await AssertRoundTripAsync(caseId, DirectChatSamples.Create(), route, expected, condition);
    }

    [Theory(DisplayName = "V02-External-03: New inputs receive distinct interaction and message IDs")]
    [Trait("Case", "V02-External-03")]
    [InlineData("V02-External-03-05", "New event and message IDs with identical text receive distinct interaction and input message IDs")]
    public async Task NewInput_ReceivesDistinctIds(string caseId, string condition)
    {
        var first = DirectChatSamples.Create();
        var next = first with { EventId = GeneratedValues.Id("other-event"),
            MessageId = GeneratedValues.Id("other-input"), ReplyToken = GeneratedValues.Id("other-reply") };
        Trace(caseId, new { condition, First = first, Next = next, FreshHosts = true });
        // Separate hosts isolate process-local locks; no conversation persistence is asserted.
        var a = await AssertRoundTripAsync(caseId + "/first", first);
        var b = await AssertRoundTripAsync(caseId + "/next", next);
        Assert.NotEqual(a.Interaction, b.Interaction);
        Assert.NotEqual(a.Message, b.Message);
    }

    private async Task<DirectChatIds> AssertRoundTripAsync(string caseId, DirectChatValues values, string route = "endpoint-default",
        string expected = "https://chat.test/chat/v0.2/generate-replies", string? condition = null)
    {
        using var chat = new CaptureHandler((sent, _) => Task.FromResult(DirectChatSamples.Ok(sent, values)));
        using var line = new CaptureHandler((_, _) => Task.FromResult(DirectChatSamples.LineOk()));
        using var host = new DirectChatGatewayFactory(values, chat, line);
        if (route == "endpoint") { host.Settings["Chat:Http:Endpoint"] = "/custom/direct"; host.Settings["Chat:Http:EndpointTemplate"] = "/unused/{version}"; }
        if (route is "template" or "default") host.Settings["Chat:Http:Endpoint"] = "";
        if (route == "template") host.Settings["Chat:Http:EndpointTemplate"] = "/custom/{version}/direct";
        if (route == "default") host.Settings["Chat:Http:EndpointTemplate"] = "";
        if (route == "absolute") { host.Settings["Chat:Http:Endpoint"] = expected; host.Settings["Chat:Http:BaseUrl"] = ""; host.Settings["App:BaseUrl"] = ""; }
        if (route == "app-base") { host.Settings["Chat:Http:BaseUrl"] = ""; host.Settings["App:BaseUrl"] = "https://app.test/"; }
        var body = DirectChatSamples.Body(values); var signature = DirectChatSamples.Sign(body);
        Trace(caseId, new { condition, Values = values, Settings = host.Settings,
            BodyBase64 = Convert.ToBase64String(body), Signature = signature, Endpoint = expected,
            Response = new { interaction_id = "copied from captured request", status = "ok", messages = new[] {
                new { id = values.OutputId, content = new[] { new { type = "text", text = values.ReplyText } } } } },
            LineResponse = "{\"sentMessages\":[{\"id\":\"sent-synthetic\"}]}", WaitLimitSeconds = 10, QuietWindowMilliseconds = 250 });
        using var client = host.CreateClient();
        using var request = DirectChatSamples.Incoming(body, signature);
        using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var response = await client.SendAsync(request, limit.Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var ids = DirectChatAssertions.AssertRequest(await chat.NextAsync(), values, expected);
        DirectChatAssertions.AssertReply(await line.NextAsync(), values);
        await Task.WhenAll(chat.AssertNoPendingRequestsAsync(), line.AssertNoPendingRequestsAsync());
        await host.StopAsync();
        Assert.Single(chat.Requests); Assert.Single(line.Requests);
        return ids;
    }
}
