#if PHASE2_GATEWAY_TESTS
// SPDX-License-Identifier: MIT
using System.Reflection;
using System.Text.Json;
using LineBot.Api.Chat;
using LineBot.Api.Processing;
using LineBot.Api.Queueing;
using LineBot.Api.Workers;
using LineBot.Api.Tests.InternalContracts.Boundaries.Support;
using LineBot.Api.Tests.Support;
using Xunit.Abstractions;
using Request = LineBot.Api.Contracts.V0_1.ChatServiceRequest;

namespace LineBot.Api.Tests.InternalContracts.Boundaries;

public sealed class Phase2BoundaryTestsC01(ITestOutputHelper output) : DiagnosticTest(output)
{
    [Fact(DisplayName = "C01-Internal-01-02: Input and result properties preserve the declared types and access")]
    [Trait("Case", "C01-Internal-01")]
    public void Models_PropertiesAndOutcomes_MatchContract()
    {
        var types = new[] { typeof(Request), typeof(ChatServiceResult), typeof(QueueItem), typeof(LineReplyContext),
            typeof(InputPreparationResult<Request>), typeof(ContractProcessingResult) };
        Trace("C01-Internal-01-02", new { Assembly = typeof(global::Program).Assembly.FullName, Models = types.Select(type => type.FullName) });
        Assert.True(typeof(IChatServiceRequest).IsAssignableFrom(typeof(Request)));
        foreach (var type in new[] { typeof(Request), typeof(ChatServiceResult) })
        {
            Assert.True(type.IsPublic && type.IsClass);
            Assert.NotNull(type.GetConstructor(Type.EmptyTypes));
        }
        var input = new Request();
        foreach (var member in new[] { "RequestId", "ConversationId", "AuthorUserId", "MessageText" })
            AssertSettable(input, member, "test-value");
        AssertSettable(input, "TimeoutSeconds", 20);
        AssertSettable(input, "MaxCharsPerMessage", 1000);
        var result = new ChatServiceResult();
        AssertSettable(result, "Messages", new List<string> { "text-test" });
        AssertSettable(result, "Status", "ok");
        AssertProperty(typeof(QueueItem), "Webhook", typeof(JsonElement));
        AssertProperty(typeof(QueueItem), "ConnectionId", typeof(string));
        AssertProperty(typeof(LineReplyContext), "ReplyToken", typeof(LineReplyToken));
        AssertProperty(typeof(LineReplyContext), "ChannelAccessToken", typeof(ChannelAccessToken));
        AssertProperty(typeof(LineReplyContext), "WebhookEventId", typeof(string));
        Assert.NotEqual(typeof(LineReplyToken), typeof(ChannelAccessToken));
        Assert.False(typeof(LineReplyToken).IsAbstract);
        Assert.False(typeof(ChannelAccessToken).IsAbstract);
        AssertProperty(typeof(InputPreparationResult<Request>), "ChatRequest", typeof(Request));
        AssertProperty(typeof(ContractProcessingResult), "ChatResult", typeof(ChatServiceResult));
        foreach (var type in new[] { typeof(InputPreparationResult<Request>), typeof(ContractProcessingResult) })
        {
            AssertProperty(type, "Outcome", typeof(PreparationOutcome));
            AssertProperty(type, "ReplyContext", typeof(LineReplyContext));
            AssertProperty(type, "ErrorMessage", typeof(string));
        }
        Assert.Equal(new[] { "Failed", "Ready" }, Enum.GetNames<PreparationOutcome>().OrderBy(name => name));
        Assert.Equal(new[] { "Accepted", "Full" }, Enum.GetNames<QueueWriteResult>().OrderBy(name => name));
    }

    private static bool IsVersionOrWire(Type type) => type.Namespace?.Contains(".Contracts.V0_", StringComparison.Ordinal) == true ||
        type.Namespace?.StartsWith("System.Net.Http", StringComparison.Ordinal) == true ||
        type.Namespace?.Split('.').Any(part => part == "Wire") == true;

    private static void AssertOperation(Type boundary, string name, Type result, params Type[] arguments)
    {
        Assert.True(boundary.IsPublic && boundary.IsInterface);
        var method = Assert.Single(boundary.GetMethods(), item => item.Name == name);
        Assert.Equal(result, method.ReturnType);
        var parameters = method.GetParameters();
        Assert.Equal(arguments.Append(typeof(CancellationToken)), parameters.Select(parameter => parameter.ParameterType));
        Assert.All(parameters[..^1], parameter => Assert.False(parameter.HasDefaultValue));
        Assert.True(parameters[^1].HasDefaultValue);
        Assert.Contains(parameters[^1].DefaultValue, new object?[] { null, default(CancellationToken) });
    }

    private static void AssertProperty(Type type, string name, Type propertyType)
    {
        var property = type.GetProperty(name);
        Assert.NotNull(property);
        Assert.Equal(propertyType, property.PropertyType);
        Assert.True(property.GetMethod?.IsPublic);
    }

    private static void AssertSettable<T>(object model, string name, T value)
    {
        var property = model.GetType().GetProperty(name)!;
        AssertProperty(model.GetType(), name, typeof(T));
        Assert.True(property.SetMethod?.IsPublic);
        property.SetValue(model, value);
        Assert.Equal(value, Assert.IsType<T>(property.GetValue(model)));
    }
}
#endif
