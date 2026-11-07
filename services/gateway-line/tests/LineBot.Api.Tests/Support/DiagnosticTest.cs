// SPDX-License-Identifier: MIT
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Xunit.Abstractions;
namespace LineBot.Api.Tests.Support;

public abstract class DiagnosticTest
{
    private readonly ITestOutputHelper _output;
    protected DiagnosticTest(ITestOutputHelper output) => _output = output;

    // Called before execution, so exceptions and timed-out waits retain the input too.
    // Only synthetic test data/configuration belongs here, never workstation credentials.
    protected void Trace(string caseId, object data, IConfiguration? settings = null)
    {
        _output.WriteLine(JsonSerializer.Serialize(new {
            Case = caseId,
            Data = data,
            Settings = settings?.AsEnumerable().ToDictionary(p => p.Key, p => p.Value)
        }));
    }
}
