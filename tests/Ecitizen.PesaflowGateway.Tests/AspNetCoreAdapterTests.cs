using Ecitizen.PesaflowGateway.AspNetCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ecitizen.PesaflowGateway.Tests;

public class AspNetCoreAdapterTests
{
    [Fact]
    public void AddEcitizenPesaflowGateway_RegistersServices()
    {
        var services = new ServiceCollection();

        services.AddEcitizenPesaflowGateway(config =>
        {
            config.ApiClientID = "CLIENT_TEST";
            config.ApiKey = "KEY_TEST";
            config.Secret = "SECRET_TEST";
            config.ServiceID = "SVC_TEST";
        });

        var provider = services.BuildServiceProvider();

        var client = provider.GetService<EcitizenClient>();
        Assert.NotNull(client);
        Assert.Equal("CLIENT_TEST", client!.GetGateway().ApiClientID);
    }
}
