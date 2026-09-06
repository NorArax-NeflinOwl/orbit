using Microsoft.AspNetCore.SignalR;
using Npgsql;
using Orbit.Core.LiveUpdates;

namespace Orbit.Api.LiveUpdates;

public static class LiveUpdatesServiceCollectionExtensions
{
    /// <summary>
    /// Registers the live connection and makes Orbit.Core's announcements come out of it.
    ///
    /// Order against AddOrbitCore does not matter, and that is on purpose. Core registers its
    /// do-nothing publisher with TryAdd, which stands down if this ran first; this one registers
    /// plainly, and the last registration is what a single resolve returns if it ran second. Either way
    /// the real publisher wins - a silent regression back to no live updates is exactly the kind of
    /// thing a reordering would otherwise cause, with no error anywhere to show for it.
    ///
    /// The announcements reach every API instance rather than only this one, because an IHubContext
    /// knows nothing of the connections another replica is holding - see
    /// <see cref="PostgresLiveUpdateFanOut"/>. That is unconditional: it costs a NOTIFY per announcement
    /// on a single replica, and the alternative is a setting whose wrong value shows up as chat that is
    /// merely slow, on the day somebody raises the replica count.
    /// </summary>
    public static IServiceCollection AddOrbitLiveUpdates(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSignalR();
        services.AddSingleton<IUserIdProvider, SubjectClaimUserIdProvider>();

        services.AddSingleton<LiveUpdateInstance>();
        services.AddSingleton<ILocalLiveUpdateFanOut, SignalRLiveUpdateFanOut>();
        services.AddSingleton(_ => new NpgsqlDataSourceBuilder(ConnectionString(configuration)).Build());
        services.AddSingleton<ILiveUpdateFanOut, PostgresLiveUpdateFanOut>();
        services.AddSingleton<ILiveUpdatePublisher, LiveUpdateAnnouncer>();
        services.AddHostedService<PostgresLiveUpdateRelay>();

        return services;
    }

    /// <summary>
    /// The same connection AddOrbitData insists on, read the same way. Missing is not handled here
    /// because it cannot happen: AddOrbitData throws on it a line later, with the message that explains
    /// how to set it.
    /// </summary>
    private static string ConnectionString(IConfiguration configuration)
        => configuration.GetConnectionString("Orbit")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:Orbit is not configured, and live updates need it to reach the "
                + "other API instances. See AddOrbitData for how to set it.");
}
