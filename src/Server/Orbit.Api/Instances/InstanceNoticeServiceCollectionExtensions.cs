using Npgsql;

namespace Orbit.Api.Instances;

public static class InstanceNoticeServiceCollectionExtensions
{
    /// <summary>
    /// Registers the channel API instances use to tell each other things - see
    /// <see cref="IInstanceNoticeHandler"/>. Handlers are registered by whoever owns them and picked up
    /// here by the listener.
    ///
    /// Unconditional rather than switched on when replicas are raised. It costs one pinned PostgreSQL
    /// connection and a NOTIFY per notice on a single instance, and the alternative is a setting whose
    /// wrong value shows up as a stale cache and a chat that is merely slow, on the day somebody scales
    /// out - neither of which looks like a misconfiguration to the person who did it.
    /// </summary>
    public static IServiceCollection AddOrbitInstanceNotices(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<InstanceIdentity>();
        services.AddSingleton(_ => new NpgsqlDataSourceBuilder(ConnectionString(configuration)).Build());
        services.AddSingleton<PostgresInstanceNoticeSender>();
        services.AddHostedService<PostgresInstanceNoticeListener>();
        return services;
    }

    /// <summary>
    /// The same connection AddOrbitData insists on, read the same way. Missing is not handled here
    /// because it cannot happen: AddOrbitData throws on it, with the message that explains how to set it.
    /// </summary>
    private static string ConnectionString(IConfiguration configuration)
        => configuration.GetConnectionString("Orbit")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:Orbit is not configured, and the API instances need it to reach each "
                + "other. See AddOrbitData for how to set it.");
}
