using Microsoft.Extensions.Options;

namespace Orbit.Api.HealthChecks;

public static class HealthChecksServiceCollectionExtensions
{
    /// <summary>
    /// Registers the health check services and binds <see cref="HealthCheckSettings"/> to the
    /// "HealthChecks" configuration section through <see cref="IOptionsMonitor{TOptions}"/>, so editing
    /// appsettings.json takes effect on the next probe without restarting the API.
    /// </summary>
    public static IServiceCollection AddOrbitHealthChecks(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<HealthCheckSettings>(configuration.GetSection("HealthChecks"));
        services.AddHttpClient();
        services.AddSingleton<ExternalServiceProber>();
        services.AddSingleton<HostedServiceHealthTracker>();

        services.AddHealthChecks()
            // Deliberately not tagged "ready": a configuration gap means a feature silently degrades,
            // which must show up on /health without pulling replicas out of rotation on /health/ready.
            .AddCheck<ConfigurationHealthCheck>("configuration")
            .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"])
            .AddCheck<DiskSpaceHealthCheck>("disk-space", tags: ["ready"])
            .AddCheck<ExternalServicesHealthCheck>("external-services", tags: ["ready"])
            .AddCheck<HostedServicesHealthCheck>("hosted-services", tags: ["ready"]);

        return services;
    }
}
