// SPDX-License-Identifier: MIT
using LineBot.Api.ConnectionManagement;

namespace LineBot.Api.Contracts.V0_2;

public sealed class V02ConnectionManagementClient(
    [Microsoft.Extensions.DependencyInjection.FromKeyedServices("connection-0.2")] HttpClient client,
    IConfiguration configuration)
    : ConnectionManagementClient(client, configuration, "0.2");
