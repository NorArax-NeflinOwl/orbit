using Microsoft.AspNetCore.SignalR;
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
    /// </summary>
    public static IServiceCollection AddOrbitLiveUpdates(this IServiceCollection services)
    {
        services.AddSignalR();
        services.AddSingleton<IUserIdProvider, SubjectClaimUserIdProvider>();
        services.AddSingleton<ILiveUpdatePublisher, SignalRLiveUpdatePublisher>();
        return services;
    }
}
