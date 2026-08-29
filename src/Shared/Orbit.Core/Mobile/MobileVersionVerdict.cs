namespace Orbit.Core.Mobile;

/// <summary>
/// What a mobile client should do about its own version, decided by <see cref="MobileVersionPolicy"/>.
/// Unlike a web deployment, an installed app cannot be rolled back or replaced from the server side -
/// old builds keep running until their owner updates - so the server needs a way to tell one it may no
/// longer run.
/// </summary>
public enum MobileVersionVerdict
{
    /// <summary>Run normally.</summary>
    Supported,

    /// <summary>Run, but a newer version exists - worth offering, not worth insisting on.</summary>
    UpdateAvailable,

    /// <summary>
    /// Stop at the splash screen and send the user to update. Reserved for versions the server can no
    /// longer support (a changed sync or encryption contract, a breaking API change), because it takes
    /// the app away from someone who was using it.
    /// </summary>
    UpdateRequired
}
