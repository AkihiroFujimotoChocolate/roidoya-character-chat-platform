// SPDX-License-Identifier: MIT
using LineBot.Api.Models;
using LineBot.Api.Services;
using LineBot.Api.Tests.InternalContracts.Boundaries.Support;
namespace LineBot.Api.Tests.InternalContracts.Boundaries;

public sealed class ChatC02BoundaryTests
{
    [Fact(DisplayName = "C02-Internal-02-01: Chat input excludes protocol version and transport details")]
    [Trait("Case", "C02-Internal-02")]
    public void ChatInput_Contract_ExcludesVersionAndTransport()
    {
        // Arrange
        var method = typeof(IChatService).GetMethod(nameof(IChatService.GenerateReplyAsync))!;
        // Assert
        Assert.Equal(typeof(ChatServiceRequest), method.GetParameters()[0].ParameterType);
        Assert.Equal(typeof(CancellationToken), method.GetParameters()[1].ParameterType);
        foreach (var property in typeof(ChatServiceRequest).GetProperties()) {
            Assert.DoesNotContain(property.Name, new[] { "ApiVersion", "Version", "MessageLanguage", "HttpRequest", "Headers", "Url" });
            Assert.DoesNotContain(CompiledDependencies.Expand(property.PropertyType), IsTransportOrWire);
        }
    }

    [Fact(DisplayName = "C02-Internal-02-02: Chat result excludes unused wire metadata and transport details")]
    [Trait("Case", "C02-Internal-02")]
    public void ChatResult_Contract_ExcludesUnusedWireMetadata()
    {
        // Assert
        Assert.Equal(typeof(Task<ChatServiceResult>),
            typeof(IChatService).GetMethod(nameof(IChatService.GenerateReplyAsync))!.ReturnType);
        Assert.NotNull(typeof(ChatServiceResult).GetProperty(nameof(ChatServiceResult.Messages)));
        Assert.NotNull(typeof(ChatServiceResult).GetProperty(nameof(ChatServiceResult.Status)));
        foreach (var property in typeof(ChatServiceResult).GetProperties()) {
            Assert.DoesNotContain(property.Name, new[] { "RequestId", "FallbackUsed", "Error" });
            Assert.DoesNotContain(CompiledDependencies.Expand(property.PropertyType), IsTransportOrWire);
        }
    }

    [Fact(DisplayName = "C02-Internal-02-03: Worker depends on abstract services without interpreting wire details")]
    [Trait("Case", "C02-Internal-02")]
    public void Worker_CompiledDependencies_UseAbstractBoundaries()
    {
        // Arrange / Act
        var types = CompiledDependencies.Types(typeof(MessageWorkerService)).Distinct().ToArray();
        // Assert
        Assert.Contains(typeof(IChatService), types);
        Assert.Contains(typeof(ILineReplyService), types);
        Assert.DoesNotContain(types, IsTransportOrWire);
        Assert.DoesNotContain(typeof(HttpV01ChatService), types);
        var uses = CompiledDependencies.Inspect(typeof(MessageWorkerService)).ToArray();
        Assert.DoesNotContain(uses, u => u.Literal is "Chat:ApiVersion" or "ok" or "provider_error" or "content_filtered");
        Assert.DoesNotContain(uses, u => u.Literal?.StartsWith("Chat:Http:", StringComparison.Ordinal) == true);
    }

    [Fact(DisplayName = "C02-Internal-01: Abstract services and boundary types expose the declared contract")]
    [Trait("Case", "C02-Internal-01")]
    public void Boundaries_Declarations_MatchContract()
    {
        foreach (var type in new[] { typeof(IChatService), typeof(ILineReplyService) }) {
            Assert.True(type.IsPublic); Assert.True(type.IsInterface);
        }
        var chat = typeof(IChatService).GetMethod("GenerateReplyAsync")!;
        Assert.Equal(typeof(Task<ChatServiceResult>), chat.ReturnType);
        Assert.Equal(new[] { typeof(ChatServiceRequest), typeof(CancellationToken) }, chat.GetParameters().Select(p => p.ParameterType));
        Assert.True(chat.GetParameters()[1].IsOptional);
        var reply = typeof(ILineReplyService).GetMethod("SendReplyAsync")!;
        Assert.Equal(typeof(Task<bool>), reply.ReturnType);
        Assert.Equal(new[] { typeof(string), typeof(List<string>), typeof(string), typeof(bool), typeof(CancellationToken) },
            reply.GetParameters().Select(p => p.ParameterType));
        Assert.True(reply.GetParameters()[4].IsOptional);
        var extra = new[] { "InteractionId", "ChannelType", "ChannelInstanceId", "ActorId", "CharacterId", "InputMessageId" };
        foreach (var type in new[] { typeof(QueueItem), typeof(ChatServiceRequest), typeof(ChatServiceResult) }) {
            Assert.True(type.IsPublic); Assert.True(type.IsClass);
            Assert.NotNull(type.GetConstructor(Type.EmptyTypes));
            var instance = Activator.CreateInstance(type)!;
            var names = type == typeof(QueueItem)
                ? new[] { "MessageText", "ReplyToken", "WebhookEventId", "UserKey", "IsRedelivery" }.Concat(extra)
                : type == typeof(ChatServiceRequest)
                    ? new[] { "MessageText", "ConversationId", "AuthorUserId", "RequestId", "TimeoutSeconds", "MaxCharsPerMessage" }.Concat(extra)
                    : new[] { "Messages", "Status" };
            foreach (var name in names) {
                var property = type.GetProperty(name);
                Assert.NotNull(property);
                var expected = name == "IsRedelivery" ? typeof(bool)
                    : name is "TimeoutSeconds" or "MaxCharsPerMessage" ? typeof(int)
                    : name == "Messages" ? typeof(List<string>) : typeof(string);
                Assert.Equal(expected, property!.PropertyType);
                Assert.True(property.GetMethod!.IsPublic); Assert.True(property.SetMethod!.IsPublic);
                object value = name switch {
                    "IsRedelivery" => true, "TimeoutSeconds" => 20, "MaxCharsPerMessage" => 1000,
                    "Messages" => new List<string> { "selected-a", "selected-b" }, "Status" => "ok",
                    "ChannelType" => "line", "InteractionId" or "InputMessageId" => Guid.NewGuid().ToString(),
                    _ => name + "-" + Guid.NewGuid().ToString("N")
                };
                property.SetValue(instance, value);
                Assert.Equal(value, property.GetValue(instance));
                if (name == "RequestId") {
                    Assert.Equal(System.Reflection.NullabilityState.Nullable,
                        new System.Reflection.NullabilityInfoContext().Create(property).ReadState);
                    property.SetValue(instance, null); Assert.Null(property.GetValue(instance));
                }
            }
        }
    }

    private static bool IsTransportOrWire(Type type) =>
        type.Namespace?.StartsWith("System.Net.Http", StringComparison.Ordinal) == true ||
        type.Namespace?.StartsWith("System.Text.Json", StringComparison.Ordinal) == true ||
        type == typeof(Uri) || type.Name.Contains("Wire", StringComparison.OrdinalIgnoreCase) ||
        type == typeof(HttpV01ChatService) || type.FullName == "LineBot.Api.Services.HttpV02ChatService";
}
