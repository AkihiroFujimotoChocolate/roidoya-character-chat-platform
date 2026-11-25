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
using Request = LineBot.Api.Contracts.V0_2.ChatServiceRequest;

namespace LineBot.Api.Tests.InternalContracts.Boundaries;

public sealed class Phase2BoundaryTests(ITestOutputHelper output) : DiagnosticTest(output)
{
    [Fact(DisplayName = "C02-Internal-01-01: Common interfaces declare the adopted signatures and generic constraints")]
    [Trait("Case", "C02-Internal-01")]
    public void Boundaries_Declarations_MatchContract()
    {
        var boundaries = new[] { typeof(ILineEventFilter), typeof(ILineWebhookEventQueue), typeof(IContractServicesResolver),
            typeof(IContractServices), typeof(IInputAssembler<Request>), typeof(IChatService<Request>), typeof(ILineReplyService) };
        Trace("C02-Internal-01-01", new { Assembly = typeof(global::Program).Assembly.FullName, Boundaries = boundaries.Select(type => type.ToString()) });

        AssertOperation(typeof(ILineEventFilter), "IsTargetAsync", typeof(Task<bool>), typeof(QueueItem));
        AssertOperation(typeof(ILineWebhookEventQueue), "TryEnqueueAsync", typeof(Task<QueueWriteResult>), typeof(QueueItem));
        AssertOperation(typeof(ILineWebhookEventQueue), "RunAsync", typeof(Task), typeof(Func<QueueItem, CancellationToken, Task>));
        AssertOperation(typeof(IContractServicesResolver), "ResolveAsync", typeof(Task<IContractServices>), typeof(QueueItem));
        AssertOperation(typeof(IContractServices), "ProcessAsync", typeof(Task<ContractProcessingResult>), typeof(QueueItem));
        AssertOperation(typeof(IInputAssembler<Request>), "PrepareAsync", typeof(Task<InputPreparationResult<Request>>), typeof(QueueItem));
        AssertOperation(typeof(IChatService<Request>), "GenerateReplyAsync", typeof(Task<ChatServiceResult>), typeof(Request));
        AssertOperation(typeof(ILineReplyService), "SendReplyAsync", typeof(Task<bool>), typeof(LineReplyContext), typeof(List<string>));

        foreach (var boundary in new[] { typeof(IInputAssembler<>), typeof(IChatService<>), typeof(ContractServices<>), typeof(InputPreparationResult<>) })
        {
            var argument = Assert.Single(boundary.GetGenericArguments());
            Assert.Equal(GenericParameterAttributes.ReferenceTypeConstraint, argument.GenericParameterAttributes & GenericParameterAttributes.SpecialConstraintMask);
            Assert.Equal(typeof(IChatServiceRequest), Assert.Single(argument.GetGenericParameterConstraints()));
        }
    }

    [Fact(DisplayName = "C02-Internal-01-02: Input and result properties preserve the declared types and access")]
    [Trait("Case", "C02-Internal-01")]
    public void Models_PropertiesAndOutcomes_MatchContract()
    {
        var types = new[] { typeof(Request), typeof(ChatServiceResult), typeof(QueueItem), typeof(LineReplyContext),
            typeof(InputPreparationResult<Request>), typeof(ContractProcessingResult) };
        Trace("C02-Internal-01-02", new { Assembly = typeof(global::Program).Assembly.FullName, Models = types.Select(type => type.FullName) });
        Assert.True(typeof(IChatServiceRequest).IsAssignableFrom(typeof(Request)));
        foreach (var type in new[] { typeof(Request), typeof(ChatServiceResult) })
        {
            Assert.True(type.IsPublic && type.IsClass);
            Assert.NotNull(type.GetConstructor(Type.EmptyTypes));
        }
        var input = new Request();
        foreach (var member in new[] { "InteractionId", "ChannelType", "ChannelInstanceId", "ActorId",
            "CharacterId", "InputMessageId", "MessageText" })
            AssertSettable(input, member, "test-value");
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

    [Fact(DisplayName = "C02-Internal-02-01: Worker processing boundaries expose only common inputs and results")]
    [Trait("Case", "C02-Internal-02")]
    public void Worker_ProcessingBoundaries_UseCommonTypes()
    {
        Trace("C02-Internal-02-01", new { Resolver = typeof(IContractServicesResolver).FullName, Processing = typeof(IContractServices).FullName });
        AssertOperation(typeof(IContractServicesResolver), "ResolveAsync", typeof(Task<IContractServices>), typeof(QueueItem));
        AssertOperation(typeof(IContractServices), "ProcessAsync", typeof(Task<ContractProcessingResult>), typeof(QueueItem));
        var exposed = new[] { typeof(QueueItem), typeof(ContractProcessingResult) }
            .SelectMany(type => type.GetProperties().SelectMany(property => CompiledDependencies.Expand(property.PropertyType)));
        Assert.DoesNotContain(exposed, IsVersionOrWire);
    }

    [Fact(DisplayName = "C02-Internal-02-02: Chat result has only shared messages and status")]
    [Trait("Case", "C02-Internal-02")]
    public void ChatResult_PublicProperties_AreMessagesAndStatus()
    {
        Trace("C02-Internal-02-02", new { Type = typeof(ChatServiceResult).FullName, Expected = new[] { "Messages", "Status" } });
        Assert.Equal(new[] { "Messages", "Status" }, typeof(ChatServiceResult).GetProperties().Select(property => property.Name).OrderBy(name => name));
        AssertProperty(typeof(ChatServiceResult), "Messages", typeof(List<string>));
        AssertProperty(typeof(ChatServiceResult), "Status", typeof(string));
        Assert.DoesNotContain(CompiledDependencies.Types(typeof(ChatServiceResult)), IsVersionOrWire);
    }

    [Fact(DisplayName = "C02-Internal-02-03: Worker delegates version selection and wire conversion")]
    [Trait("Case", "C02-Internal-02")]
    public void Worker_CompiledDependencies_UseAbstractBoundaries()
    {
        var worker = typeof(MessageWorkerService);
        Trace("C02-Internal-02-03", new { Worker = worker.FullName, Expected = new[] {
            "ILineWebhookEventQueue", "IContractServicesResolver", "IContractServices", "ILineReplyService" } });
        var types = CompiledDependencies.Types(worker).Distinct().ToArray();
        Trace("C02-Internal-02-03-observed", new { Dependencies = types.Select(type => type.FullName).ToArray() });
        Assert.Contains(typeof(ILineWebhookEventQueue), types);
        Assert.Contains(typeof(IContractServicesResolver), types);
        Assert.Contains(typeof(IContractServices), types);
        Assert.Contains(typeof(ILineReplyService), types);
        Assert.DoesNotContain(types, IsVersionOrWire);
        Assert.DoesNotContain(types, type => type.Name is "LocalLineWebhookEventQueue" or "ContractServiceResolver" or "LineReplyService" or "ContractServices`1");
        Assert.DoesNotContain(CompiledDependencies.Inspect(worker), use => use.Literal is "ContractVersion" or "0.1" or "0.2");
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
