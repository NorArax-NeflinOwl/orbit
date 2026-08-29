using Orbit.Core.Mobile;

namespace Orbit.Api.Config;

/// <summary>
/// Per-platform version rules for the mobile apps, bound from the "MobileVersion" configuration section.
/// Nothing here is secret - these are release numbers and public store links - so unlike the Jwt and
/// Vapid sections these may live in a committed appsettings file.
///
/// Left unset, every version is supported and the forced-update gate never fires.
/// </summary>
public sealed class MobileVersionSettings
{
    public const string SectionName = "MobileVersion";

    public MobilePlatformVersionSettings Ios { get; set; } = new();
    public MobilePlatformVersionSettings Android { get; set; } = new();

    public MobilePlatformVersionSettings For(MobilePlatform platform)
        => platform == MobilePlatform.Ios ? Ios : Android;
}

/// <summary>One platform's release numbers and where to send someone to update.</summary>
public sealed class MobilePlatformVersionSettings
{
    /// <summary>The oldest build still allowed to run. Empty means no minimum, so nothing is ever blocked.</summary>
    public string MinimumSupportedVersion { get; set; } = string.Empty;

    /// <summary>The newest released build, used to offer a non-blocking update. Empty means none is offered.</summary>
    public string LatestVersion { get; set; } = string.Empty;

    /// <summary>The store listing to open when the app asks the user to update.</summary>
    public string UpdateUrl { get; set; } = string.Empty;

    public MobileVersionPolicy ToPolicy()
        => MobileVersionPolicy.FromConfiguredValues(MinimumSupportedVersion, LatestVersion);
}
