// SPDX-License-Identifier: MIT
using System.Net;
using LineBot.Api.Tests.Support;
using LineBot.Api.Tests.ExternalContracts.V02.Support;
using Xunit.Abstractions;

namespace LineBot.Api.Tests.ExternalContracts.V02;

public sealed class AdmissionContractTests : DiagnosticTest
{
    public AdmissionContractTests(ITestOutputHelper output) : base(output) { }

    [Theory(DisplayName = "V02-External-04: Admission preserves existing standby and non-text exclusions")]
    [Trait("Case", "V02-External-04")]
    [InlineData("V02-External-04-02", "A standby text event produces no outbound requests", "standby", "user", "message", "text")]
    [InlineData("V02-External-04-05", "A follow event produces no outbound requests", "active", "user", "follow", "text")]
    [InlineData("V02-External-04-06", "An image event produces no outbound requests", "active", "user", "message", "image")]
    public async Task OutOfProfileInput_DoesNotReachChatOrLine(string caseId, string condition,
        string mode, string source, string eventType, string messageType)
    {
        var v = DirectChatSamples.Create();
        var control = v with {
            UserId = "U" + Guid.NewGuid().ToString("N"),
            EventId = GeneratedValues.Id("control-event"), ReplyToken = GeneratedValues.Id("control-reply"),
            MessageId = GeneratedValues.Id("control-input")
        };
        using var chat = new CaptureHandler((sent, _) => Task.FromResult(DirectChatSamples.Ok(sent, v)));
        using var line = new CaptureHandler((_, _) => Task.FromResult(DirectChatSamples.LineOk()));
        using var host = new DirectChatGatewayFactory(v, chat, line);
        var body = DirectChatSamples.Body(v, mode, source, eventType, messageType);
        var signature = DirectChatSamples.Sign(body);
        var controlBody = DirectChatSamples.Body(control);
        var controlSignature = DirectChatSamples.Sign(controlBody);
        Trace(caseId, new { condition, Values = v, Settings = host.Settings,
            BodyBase64 = Convert.ToBase64String(body), Signature = signature,
            ControlValues = control, ControlBodyBase64 = Convert.ToBase64String(controlBody), ControlSignature = controlSignature,
            ExpectedControlChatRequests = 1, ExpectedControlLineRequests = 1,
            ExpectedStatus = 200, ExpectedChatRequests = 0, ExpectedLineRequests = 0,
            ChatResponse = DirectChatSamples.ResponseDescription(v),
            LineResponse = "{\"sentMessages\":[{\"id\":\"sent-synthetic\"}]}",
            WaitLimitSeconds = 10, QuietWindowMilliseconds = 250 });
        using var client = host.CreateClient();
        // Prove this host's normal path works before asserting that the excluded input sends nothing.
        using (var controlRequest = DirectChatSamples.Incoming(controlBody, controlSignature))
        using (var controlLimit = new CancellationTokenSource(TimeSpan.FromSeconds(10))) {
            using var controlResponse = await client.SendAsync(controlRequest, controlLimit.Token);
            Assert.Equal(HttpStatusCode.OK, controlResponse.StatusCode);
            DirectChatAssertions.AssertRequest(await chat.NextAsync(), control, "https://chat.test/chat/v0.2/generate-replies");
            DirectChatAssertions.AssertReply(await line.NextAsync(), control);
        }
        using var request = DirectChatSamples.Incoming(body, signature);
        using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var response = await client.SendAsync(request, limit.Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await Task.WhenAll(chat.AssertNoPendingRequestsAsync(), line.AssertNoPendingRequestsAsync());
        await host.StopAsync(); Assert.Single(chat.Requests); Assert.Single(line.Requests);
    }

    [Theory(DisplayName = "V02-External-05: LINE signature verification remains before processing")]
    [Trait("Case", "V02-External-05")]
    [InlineData("V02-External-05-01", "Missing LINE signature returns 400 with no outbound requests", "missing")]
    [InlineData("V02-External-05-02", "A signature using another secret returns 400 with no outbound requests", "wrong-secret")]
    [InlineData("V02-External-05-03", "Changing the body after signing returns 400 with no outbound requests", "changed-body")]
    public async Task InvalidSignature_IsRejected(string caseId, string condition, string kind)
    {
        var v = DirectChatSamples.Create();
        using var chat = new CaptureHandler((sent, _) => Task.FromResult(DirectChatSamples.Ok(sent, v)));
        using var line = new CaptureHandler((_, _) => Task.FromResult(DirectChatSamples.LineOk()));
        using var host = new DirectChatGatewayFactory(v, chat, line);
        var original = DirectChatSamples.Body(v);
        var sent = kind == "changed-body" ? original.Concat(new byte[] { 32 }).ToArray() : original;
        var signature = kind == "missing" ? null : DirectChatSamples.Sign(original,
            kind == "wrong-secret" ? "another-synthetic-secret" : DirectChatSamples.Secret);
        Trace(caseId, new { condition, Values = v, Settings = host.Settings,
            OriginalBodyBase64 = Convert.ToBase64String(original), BodyBase64 = Convert.ToBase64String(sent), Signature = signature,
            ExpectedStatus = 400, ExpectedChatRequests = 0, ExpectedLineRequests = 0,
            UnusedChatResponse = DirectChatSamples.ResponseDescription(v),
            UnusedLineResponse = "{\"sentMessages\":[{\"id\":\"sent-synthetic\"}]}",
            WaitLimitSeconds = 10, QuietWindowMilliseconds = 250 });
        using var client = host.CreateClient(); using var request = DirectChatSamples.Incoming(sent, signature);
        using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var response = await client.SendAsync(request, limit.Token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await Task.WhenAll(chat.AssertNoPendingRequestsAsync(), line.AssertNoPendingRequestsAsync());
        await host.StopAsync(); Assert.Empty(chat.Requests); Assert.Empty(line.Requests);
    }
}
