namespace Orbit.Mobile.Sync;

/// <summary>
/// Whether the phone currently believes it can reach Orbit. The app answers this with
/// <see cref="ServerReachability"/>, which reads MAUI's Connectivity and also knows when the deployment
/// has said it is paused - a phone on a working network with the server stopped is offline in every
/// sense a screen cares about. It is an interface here so the offline rules can be tested without either.
///
/// It is a belief, not a guarantee - a connected phone on a captive portal reaches nothing. Treat it as
/// good enough to decide what to *offer* the user, never as a reason to skip handling a failed request.
/// </summary>
public interface INetworkStatus
{
    bool IsOnline { get; }

    /// <summary>
    /// Raised when the answer changes. A screen that greys out what needs a connection has to hear about
    /// it: an action left disabled after the connection came back is as wrong as one offered while it
    /// could only fail, and nobody thinks to leave the screen and come back.
    /// </summary>
    event EventHandler? Changed;
}
