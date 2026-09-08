using System;
using System.Net.Http;
using Ecitizen.PesaflowGateway;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Ecitizen.PesaflowGateway.AspNetCore;

/// <summary>
/// Extension methods for setting up eCitizen / PesaFlow Gateway in an <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    public const string DefaultConfigurationSection = "Ecitizen";

    /// <summary>
    /// Adds EcitizenClient and related services to the DI container.
    /// </summary>
    public static IServiceCollection AddEcitizenPesaflowGateway(
        this IServiceCollection services,
        Action<EcitizenConfig>? configure = null)
    {
        if (configure != null)
        {
            services.Configure(configure);
        }
        else
        {
            services.AddOptions<EcitizenConfig>();
        }

        services.AddHttpClient(nameof(EcitizenClient));

        services.AddSingleton(sp =>
        {
            var options = sp.GetService<IOptions<EcitizenConfig>>()?.Value ?? new EcitizenConfig();
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient(nameof(EcitizenClient));
            return new EcitizenClient(options, httpClient);
        });

        return services;
    }

    /// <summary>
    /// Adds EcitizenClient binding configuration from the specified IConfiguration section.
    /// </summary>
    public static IServiceCollection AddEcitizenPesaflowGateway(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = DefaultConfigurationSection)
    {
        var section = configuration.GetSection(sectionName);
        services.Configure<EcitizenConfig>(section);

        services.AddHttpClient(nameof(EcitizenClient));

        services.AddSingleton(sp =>
        {
            var options = sp.GetService<IOptions<EcitizenConfig>>()?.Value ?? new EcitizenConfig();
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient(nameof(EcitizenClient));
            return new EcitizenClient(options, httpClient);
        });

        return services;
    }
}
