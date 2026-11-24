// SPDX-License-Identifier: MIT
using LineBot.Api.ConnectionManagement;

namespace LineBot.Api.Contracts.V0_1;

public sealed class V01ConnectionManagementClient(
    [Microsoft.Extensions.DependencyInjection.FromKeyedServices("connection-0.1")] HttpClient client,
    IConfiguration configuration)
    : ConnectionManagementClient(client, configuration, "0.1");
