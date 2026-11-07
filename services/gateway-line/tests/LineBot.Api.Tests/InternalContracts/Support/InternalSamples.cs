// SPDX-License-Identifier: MIT
using Microsoft.Extensions.Configuration;

namespace LineBot.Api.Tests.InternalContracts.Support;

internal static class InternalSamples
{
    internal static IConfiguration Config(params (string Key, string? Value)[] settings)
    {
        var values = new Dictionary<string, string?> { ["Chat:Http:BaseUrl"] = "https://chat.test/",
            ["Chat:ApiVersion"] = "0.1", ["Line:ChannelAccessToken"] = "access-token-test",
            ["Chat:Fallbacks:Default"] = "default-test", ["Reply:Qps"] = "100" };
        foreach (var (key, value) in settings) values[key] = value;
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}
