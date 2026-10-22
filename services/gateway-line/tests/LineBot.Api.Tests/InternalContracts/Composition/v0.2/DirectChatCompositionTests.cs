// SPDX-License-Identifier: MIT
using LineBot.Api.Models;
using LineBot.Api.Services;
using LineBot.Api.Tests.InternalContracts.Support;
using LineBot.Api.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit.Abstractions;

namespace LineBot.Api.Tests.InternalContracts.Composition.V02;

public sealed class DirectChatCompositionTests : DiagnosticTest
{
    public DirectChatCompositionTests(ITestOutputHelper output) : base(output) { }

    [Theory(DisplayName = "V02-Internal-02: One application assembly selects either adapter in production composition")]
    [Trait("Case", "V02-Internal-02")]
    [InlineData("V02-Internal-02-01", "Selecting 0.1 resolves the existing adapter without Character ID", "0.1", "LineBot.Api.Services.HttpV01ChatService")]
    [InlineData("V02-Internal-02-02", "Selecting 0.2 resolves the new adapter", "0.2", "LineBot.Api.Services.HttpV02ChatService")]
    public async Task StartupVersion_SelectsAdapter(string caseId, string condition, string version, string expectedType)
    {
        using var factory = new DirectChatCompositionFactory();
        factory.Settings["Chat:ApiVersion"] = version;
        if (version == "0.1") { factory.Settings["Character:Id"] = ""; factory.Settings["Chat:Http:Endpoint"] = "/chat/v0.1/generate-replies"; }
        Trace(caseId, new { condition, Settings = factory.Settings, ExpectedType = expectedType,
            ApplicationAssembly = typeof(IChatService).Assembly.FullName, WaitLimitSeconds = 10 });
        _ = factory.Services;
        using var scope = factory.Services.CreateScope();
        var selected = scope.ServiceProvider.GetRequiredService<IChatService>();
        Assert.Equal(expectedType, selected.GetType().FullName);
        Assert.Contains(factory.Services.GetServices<IHostedService>(), s => s is MessageWorkerService);
        // Both types must exist in the already loaded application assembly.
        Assert.NotNull(typeof(IChatService).Assembly.GetType("LineBot.Api.Services.HttpV01ChatService"));
        Assert.NotNull(typeof(IChatService).Assembly.GetType("LineBot.Api.Services.HttpV02ChatService"));
        await factory.StopWorkerAsync();
    }

    [Fact(DisplayName = "V02-Internal-03: Worker forwards added normalized identity through the existing abstract service")]
    [Trait("Case", "V02-Internal-03")]
    public async Task Worker_ForwardsAddedIdentityWithoutWireTypes()
    {
        var values = FlowSamples.CreateRandom();
        var additions = new Dictionary<string, string> {
            ["InteractionId"] = GeneratedValues.Id("interaction"), ["ServiceId"] = "default", ["ChannelType"] = "line",
            ["ChannelInstanceId"] = "bot:internal-test", ["ActorId"] = "line-user-U11111111111111111111111111111111",
            ["ConversationId"] = "default",
            ["CharacterId"] = "character:internal-test", ["InputMessageId"] = GeneratedValues.Id("input-message")
        };
        var job = values.Job();
        // Reflection permits the test suite to compile before the additive production fields exist.
        var chat = new ChatDouble(); var reply = new ReplyDouble();
        using var factory = new DirectChatCompositionFactory { ChatOverride = chat, ReplyOverride = reply };
        Trace("V02-Internal-03", new { Values = values, AddedFields = additions, Settings = factory.Settings,
            ChatStatus = "ok", ChatMessages = new[] { "selected-a", "selected-b" }, ReplyResult = true, WaitLimitSeconds = 10 });
        foreach (var (name, value) in additions) AssertAddedStringAndSet(job, name, value);
        _ = factory.Services;
        factory.Queue.TryEnqueue(job);
        await factory.Queue.AfterJob.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await factory.StopWorkerAsync();
        var call = Assert.Single(chat.Calls);
        Assert.Equal(values.Text, call.Request.MessageText);
        Assert.Equal("default", call.Request.ConversationId);
        foreach (var (name, expected) in additions) {
            var property = typeof(ChatServiceRequest).GetProperty(name);
            Assert.NotNull(property); Assert.Equal(typeof(string), property!.PropertyType);
            Assert.Equal(expected, property.GetValue(call.Request));
        }
        Assert.Single(reply.Calls);
    }

    private static void AssertAddedStringAndSet(object target, string name, string value)
    {
        var property = target.GetType().GetProperty(name);
        Assert.NotNull(property); Assert.True(property!.CanWrite);
        Assert.Equal(typeof(string), property.PropertyType); property.SetValue(target, value);
    }
}
