#if PHASE2_GATEWAY_TESTS
// SPDX-License-Identifier: MIT
using System.Net;
using System.Text.Json;
using LineBot.Api.Tests.Support;
using LineBot.Api.Tests.ExternalContracts.V01.Support;
using LineBot.Api.Tests.ExternalContracts.V01.Chat;
using LineBot.Api.Tests.ExternalContracts.V01.ConnectionManagement;
using LineBot.Api.Tests.ExternalContracts.V01.Line;
using Xunit.Abstractions;

namespace LineBot.Api.Tests.ExternalContracts.V01.Line;

public sealed class WebhookQueueContractTests(ITestOutputHelper output) : ExternalFlowTest(output)
{
    [Theory(DisplayName = "C01-External-31: Additional or batched events are accepted and correlated")]
    [Trait("Case", "C01-External-31")]
    [InlineData("C01-External-31-01", "Accept B while the reply to A is held", false)]
    [InlineData("C01-External-31-02", "Deliver each reply from one two-event webhook", true)]
    public async Task Webhook_AdditionalEvents_PreserveEachReply(string caseId, string description, bool batch)
    {
        var first = ExternalSamples.Random();
        var second = ExternalSamples.Random() with { ConnectionId = first.ConnectionId,
            CharacterId = first.CharacterId, Destination = first.Destination };
        var inputs = new[] { first, second };
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var chat = new CaptureHandler(async (sent, token) =>
        {
            var input = inputs.Single(item => item.Text == ChatText(sent));
            if (!batch && input == first)
                await release.Task.WaitAsync(token);
            return ChatV01Samples.Ok(input.ReplyText);
        });
        using var line = new CaptureHandler((_, _) => Task.FromResult(LineReplySamples.SuccessResponse()));
        using var connections = ConnectionSamples.Capture(first, person => inputs.Single(item => item.PersonId == person));
        using var host = new GatewayApplicationFactory(chat, line, connections);
        if (!batch) host.Settings["Chat:RequestTimeoutSeconds"] = "60";
        var bodies = batch
            ? new[] { LineWebhookSamples.WebhookWithEvents(first.Destination, first.Body(), second.Body()) }
            : inputs.Select(input => input.Body()).ToArray();
        for (var i = 0; i < inputs.Length; i++)
        {
            var body = batch ? bodies[0] : bodies[i];
            Record(caseId, host, inputs[i], body, LineWebhookSamples.Sign(body),
                new { Description = description, Inputs = inputs, ExpectedChat = 2, ExpectedLine = 2, HoldFirst = !batch },
                connection: first);
        }
        using var client = host.CreateClient();
        try
        {
            await AssertWebhookAcceptedAsync(client, first, bodies[0]);
            if (!batch)
            {
                ChatV01Assertions.AssertRequest(await chat.NextAsync(), first, host.ChatUri.AbsoluteUri);
                await AssertWebhookAcceptedAsync(client, second, bodies[1]);
                release.TrySetResult();
                ChatV01Assertions.AssertRequest(await chat.NextAsync(), second, host.ChatUri.AbsoluteUri);
            }
            else
            {
                for (var i = 0; i < inputs.Length; i++)
                {
                    var sent = await chat.NextAsync();
                    ChatV01Assertions.AssertRequest(sent, inputs.Single(input => input.Text == ChatText(sent)), host.ChatUri.AbsoluteUri);
                }
            }
            for (var i = 0; i < inputs.Length; i++)
            {
                var sent = await line.NextAsync();
                var input = inputs.Single(item => item.ReplyToken == ReplyToken(sent));
                LineReplyAssertions.AssertRequest(sent, input, input.ReplyText);
            }
            await Task.WhenAll(chat.AssertNoPendingRequestsAsync(), line.AssertNoPendingRequestsAsync());
            await host.StopHostAsync();
            Assert.Equal(2, chat.Requests.Count);
            Assert.Equal(2, line.Requests.Count);
            foreach (var input in inputs)
            {
                Assert.Single(chat.Requests, sent => ChatText(sent) == input.Text);
                Assert.Single(line.Requests, sent => ReplyToken(sent) == input.ReplyToken);
            }
            ConnectionSamples.AssertRequests(host, first, inputs);
            Trace(caseId + "-observed", new { Chat = chat.Requests, Line = line.Requests, Connection = connections.Requests });
        }
        finally
        {
            release.TrySetResult();
            await host.StopHostAsync();
        }
    }

    [Theory(DisplayName = "C01-External-32: A full queue replies as configured and recovers")]
    [Trait("Case", "C01-External-32")]
    [InlineData("C01-External-32-01", "Hold the fixed reply and observe pending webhook", new[] { "  満杯です  " }, false, true, null)]
    [InlineData("C01-External-32-02", "Accept either configured text with whitespace retained", new[] { "  満杯A  ", "満杯B" }, false, false, null)]
    [InlineData("C01-External-32-03", "Use an unset fixed-reply setting", null, false, false, null)]
    [InlineData("C01-External-32-04", "Use an empty fixed-reply array", new string[] { }, true, false, null)]
    [InlineData("C01-External-32-06", "Ignore standby beside a target event", new[] { "  満杯です  " }, false, false, "standby")]
    [InlineData("C01-External-32-07", "Account for two target events independently", new[] { "  満杯です  " }, false, false, "active")]
    public async Task Webhook_FullQueue_PreservesRepliesAndRecovers(string caseId, string description,
        string[]? configuredReplies, bool emptyArray, bool holdFixedReply, string? firstMixedEventMode)
    {
        const int limit = 64;
        const string fixedText = "  満杯です  ";
        var silent = configuredReplies is null or { Length: 0 };
        var first = ExternalSamples.Random();
        ExternalValues NextInput() => ExternalSamples.Random() with { ConnectionId = first.ConnectionId,
            Destination = first.Destination, CharacterId = first.CharacterId };
        var additional = Enumerable.Range(0, limit).Select(_ => NextInput()).ToArray();
        var mixed = new[] { NextInput(), NextInput() };
        var recovery = NextInput();
        var all = additional.Concat(mixed).Append(first).Append(recovery).ToArray();
        var byText = all.ToDictionary(input => input.Text);
        var byPerson = all.ToDictionary(input => input.PersonId);
        var leadingBody = first.Body();
        var recoveryBody = recovery.Body();
        var mixedBody = LineWebhookSamples.WebhookWithEvents(first.Destination,
            mixed[0].Body(mode: firstMixedEventMode ?? "active"), mixed[1].Body());
        var additions = additional.Select(input => input.Body()).ToArray();

        // The silent cases use the same inputs and queue settings in a fixed-reply control first.
        foreach (var control in silent ? new[] { true, false } : new[] { false })
        {
            var allowed = control ? new[] { fixedText } : configuredReplies ?? [];
            var usesFixed = allowed.Length > 0;
            var releaseChat = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseLine = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var chat = new CaptureHandler(async (sent, token) =>
            {
                var input = byText[ChatText(sent)];
                await releaseChat.Task.WaitAsync(token);
                return ChatV01Samples.Ok(input.ReplyText);
            });
            using var line = new CaptureHandler(async (sent, token) =>
            {
                if (holdFixedReply && allowed.Contains(ReplyText(sent)))
                    await releaseLine.Task.WaitAsync(token);
                return LineReplySamples.SuccessResponse();
            });
            using var connections = ConnectionSamples.Capture(first, person => byPerson[person]);
            using var host = new GatewayApplicationFactory(chat, line, connections);
            host.Settings["Reply:QueueMaxSize"] = "1";
            host.Settings["Local:Queue:MaxSize"] = "1";
            host.Settings["Chat:RequestTimeoutSeconds"] = "60";
            if (usesFixed)
                for (var i = 0; i < allowed.Length; i++)
                    host.Settings[$"Reply:QueueFullMessages:{i}"] = allowed[i];
            else if (emptyArray)
                host.UseEmptyQueueFullMessages = true;

            Trace(caseId, new { description, Control = control, host.Settings, host.UseEmptyQueueFullMessages, Inputs = all,
                Connection = ConnectionSamples.Connection(first),
                ChatResponse = "HTTP 200, request_id correlation-test, status ok, matching reply text, fallback_used false",
                LineResponse = LineReplySamples.SuccessResponseJson, FixedReplies = usesFixed ? allowed : Array.Empty<string>(),
                WaitSeconds = 10, QuietMilliseconds = 250, MaximumAdditionalInputs = limit,
                Order = "leading, additions, optional mixed batch, release, recovery",
                Expected = "One reply per accepted or fixed-reply event; no Chat for fixed replies; recovery normal",
                WebhookPath = "/line/webhook",
                WebhookRequests = new[] { leadingBody }.Concat(additions).Append(mixedBody).Append(recoveryBody)
                    .Select(bytes => new { BodyBase64 = Convert.ToBase64String(bytes), Signature = LineWebhookSamples.Sign(bytes) }) });
            using var client = host.CreateClient();
            var submitted = new List<ExternalValues> { first };
            try
            {
                await AssertWebhookAcceptedAsync(client, first, leadingBody);
                ChatV01Assertions.AssertRequest(await chat.NextAsync(), first, host.ChatUri.AbsoluteUri);
                var fixedReached = false;
                for (var i = 0; i < additions.Length; i++)
                {
                    var input = additional[i];
                    submitted.Add(input);
                    if (holdFixedReply)
                    {
                        using var request = LineWebhookSamples.Incoming(additions[i], LineWebhookSamples.Sign(additions[i]), "/line/webhook");
                        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                        var sending = client.SendAsync(request, deadline.Token);
                        using var notification = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                        var reached = line.NextAsync(notification.Token);
                        try
                        {
                            if (await Task.WhenAny(sending, reached) == reached)
                            {
                                var fixedReply = await reached;
                                LineReplyAssertions.AssertRequest(fixedReply, input, fixedText);
                                Assert.False(sending.IsCompleted);
                                releaseLine.TrySetResult();
                                fixedReached = true;
                            }
                            using var response = await sending;
                            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                        }
                        finally
                        {
                            notification.Cancel();
                            try { await reached; } catch (OperationCanceledException) when (notification.IsCancellationRequested) { }
                            if (!sending.IsCompleted)
                            {
                                releaseLine.TrySetResult();
                                deadline.Cancel();
                                try { using var response = await sending; } catch (OperationCanceledException) { }
                            }
                        }
                    }
                    else
                    {
                        await AssertWebhookAcceptedAsync(client, first, additions[i]);
                        fixedReached = usesFixed && line.Requests.Any(sent => ReplyToken(sent) == input.ReplyToken);
                    }
                    if (fixedReached) break;
                }
                if (usesFixed) Assert.True(fixedReached, "No fixed reply was observed within 64 additional events.");
                if (firstMixedEventMode is not null)
                {
                    submitted.AddRange(mixed);
                    await AssertWebhookAcceptedAsync(client, first, mixedBody);
                }

                Trace(caseId + "-release", new { Inputs = submitted.Select(item => item.EventId).ToArray(),
                    ChatBeforeRelease = chat.Requests.ToArray(), LineBeforeRelease = line.Requests.ToArray() });
                releaseChat.TrySetResult();
                if (usesFixed)
                {
                    var repliesToRead = submitted.Count
                        - (firstMixedEventMode == "standby" ? 1 : 0)
                        - (holdFixedReply ? 1 : 0);
                    await AssertObservedRepliesAsync(chat, line, first.ReplyToken, submitted, repliesToRead);
                    await Task.WhenAll(chat.AssertNoPendingRequestsAsync(), line.AssertNoPendingRequestsAsync());
                    await AssertWebhookAcceptedAsync(client, first, recoveryBody);
                    ChatV01Assertions.AssertRequest(await chat.NextAsync(), recovery, host.ChatUri.AbsoluteUri);
                    LineReplyAssertions.AssertRequest(await line.NextAsync(), recovery, recovery.ReplyText);
                    submitted.Add(recovery);
                }
                else
                {
                    // The first Chat request was read before filling the queue.
                    var receivedReplies = new HashSet<string>(StringComparer.Ordinal);
                    await AssertReplyForObservedChatAsync(first);
                    var recovering = false;
                    while (true)
                    {
                        using var quiet = new CancellationTokenSource(recovering ? TimeSpan.FromSeconds(10) : TimeSpan.FromMilliseconds(250));
                        CapturedHttp request;
                        try { request = await chat.NextAsync(quiet.Token); }
                        catch (OperationCanceledException) when (!recovering && quiet.IsCancellationRequested)
                        {
                            await AssertWebhookAcceptedAsync(client, first, recoveryBody);
                            submitted.Add(recovery);
                            recovering = true;
                            continue;
                        }

                        var input = Assert.Single(submitted, item => item.Text == ChatText(request));
                        ChatV01Assertions.AssertRequest(request, input, host.ChatUri.AbsoluteUri);
                        await AssertReplyForObservedChatAsync(input);
                        if (input == recovery) break;
                    }

                    async Task AssertReplyForObservedChatAsync(ExternalValues input)
                    {
                        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                        while (!receivedReplies.Contains(input.ReplyToken))
                        {
                            var reply = await line.NextAsync(deadline.Token);
                            var repliedTo = Assert.Single(submitted, item => item.ReplyToken == ReplyToken(reply));
                            Assert.True(receivedReplies.Add(repliedTo.ReplyToken));
                            LineReplyAssertions.AssertRequest(reply, repliedTo, repliedTo.ReplyText);
                        }
                    }
                }
                await Task.WhenAll(chat.AssertNoPendingRequestsAsync(), line.AssertNoPendingRequestsAsync());
                await host.StopHostAsync();

                var dropped = 0;
                foreach (var input in submitted)
                {
                    var chats = chat.Requests.Where(sent => ChatText(sent) == input.Text).ToArray();
                    var replies = line.Requests.Where(sent => ReplyToken(sent) == input.ReplyToken).ToArray();
                    if (firstMixedEventMode == "standby" && input == mixed[0])
                    {
                        Assert.Empty(chats);
                        Assert.Empty(replies);
                    }
                    else if (input != first && input != recovery && replies.Length == 0 && !usesFixed)
                    {
                        Assert.Empty(chats);
                        dropped++;
                    }
                    else
                    {
                        var reply = Assert.Single(replies);
                        if (usesFixed && input != first && input != recovery && allowed.Contains(ReplyText(reply)))
                        {
                            Assert.Empty(chats);
                            LineReplyAssertions.AssertRequest(reply, input, ReplyText(reply));
                        }
                        else
                        {
                            ChatV01Assertions.AssertRequest(Assert.Single(chats), input, host.ChatUri.AbsoluteUri);
                            LineReplyAssertions.AssertRequest(reply, input, input.ReplyText);
                        }
                    }
                }
                if (!usesFixed) Assert.True(dropped > 0, "No silently rejected input was observed.");
                if (firstMixedEventMode == "standby")
                    LineReplyAssertions.AssertRequest(Assert.Single(line.Requests, sent => ReplyToken(sent) == mixed[1].ReplyToken), mixed[1], fixedText);
                Assert.All(chat.Requests, sent => Assert.Contains(submitted, input => input.Text == ChatText(sent)));
                Assert.All(line.Requests, sent => Assert.Contains(submitted, input => input.ReplyToken == ReplyToken(sent)));
                ConnectionSamples.AssertRequests(host, first,
                    submitted.Where(input => chat.Requests.Any(sent => ChatText(sent) == input.Text)).ToArray(), submitted);
                Trace(caseId + "-observed", new { Control = control, Dropped = dropped, Chat = chat.Requests, Line = line.Requests });
            }
            finally
            {
                releaseLine.TrySetResult();
                releaseChat.TrySetResult();
                await host.StopHostAsync();
            }
        }
    }

    private static async Task AssertObservedRepliesAsync(CaptureHandler chat, CaptureHandler line, string firstToken, IReadOnlyList<ExternalValues> submitted, int expected)
    {
        for (var i = 0; i < expected; i++)
        {
            var reply = await line.NextAsync();
            var input = Assert.Single(submitted, item => item.ReplyToken == ReplyToken(reply));
            if (ReplyText(reply) == input.ReplyText && input.ReplyToken != firstToken)
                await chat.NextAsync();
        }
    }

    private static async Task AssertWebhookAcceptedAsync(HttpClient client, ExternalValues connection, byte[] body)
    {
        using var request = LineWebhookSamples.Incoming(body, LineWebhookSamples.Sign(body), "/line/webhook");
        using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var response = await client.SendAsync(request, limit.Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static string ChatText(CapturedHttp request)
    {
        using var json = request.Json();
        return json.RootElement.GetProperty("message").GetProperty("text").GetString()!;
    }

    private static string ReplyToken(CapturedHttp request)
    {
        using var json = request.Json();
        return json.RootElement.GetProperty("replyToken").GetString()!;
    }

    private static string ReplyText(CapturedHttp request)
    {
        using var json = request.Json();
        return json.RootElement.GetProperty("messages")[0].GetProperty("text").GetString()!;
    }
}
#endif
