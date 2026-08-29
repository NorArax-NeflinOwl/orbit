using Orbit.Core.Mobile;

namespace Orbit.Mobile.Update;

/// <summary>
/// What startup should do about this build, and what to tell the user if it has to stop.
/// </summary>
/// <param name="LatestVersion">The newest release, when the server named one - shown in the prompt.</param>
/// <param name="UpdateUrl">Where to send the user to update. Null when this deployment hasn't configured one.</param>
public sealed record VersionGateDecision(MobileVersionVerdict Verdict, string? LatestVersion, string? UpdateUrl)
{
    /// <summary>The answer when there is nothing to act on - see MobileVersionGate for when it applies.</summary>
    public static VersionGateDecision Supported { get; } = new(MobileVersionVerdict.Supported, null, null);

    /// <summary>Startup must not continue - the splash screen holds with no way past it.</summary>
    public bool StopsTheApp => Verdict is MobileVersionVerdict.UpdateRequired;

    /// <summary>Worth offering an update the user is free to dismiss.</summary>
    public bool OffersUpdate => Verdict is MobileVersionVerdict.UpdateAvailable;
}
