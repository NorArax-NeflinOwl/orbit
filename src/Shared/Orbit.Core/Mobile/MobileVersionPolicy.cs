namespace Orbit.Core.Mobile;

/// <summary>
/// One platform's version rules: the oldest build the server still supports, and the newest one
/// released. Both are optional, and a policy with no minimum never blocks anything - a deployment that
/// hasn't configured this behaves as if the feature didn't exist, matching how SmtpSettings and
/// VapidSettings stay silent rather than failing when unconfigured.
///
/// Versions are compared with <see cref="Version"/> rather than a hand-written SemVer parser: the app's
/// ApplicationDisplayVersion is a plain numeric "major.minor.patch", which is exactly what it already
/// handles.
/// </summary>
public sealed record MobileVersionPolicy(Version? MinimumSupported, Version? Latest)
{
    /// <summary>A policy that supports everything - what an unconfigured platform gets.</summary>
    public static MobileVersionPolicy Unrestricted { get; } = new(MinimumSupported: null, Latest: null);

    /// <summary>
    /// Builds a policy from configured strings, ignoring any value that isn't a version - a typo in
    /// configuration relaxes the rule rather than locking every user out of the app.
    /// </summary>
    public static MobileVersionPolicy FromConfiguredValues(string? minimumSupported, string? latest)
        => new(ParseOrNull(minimumSupported), ParseOrNull(latest));

    /// <param name="reportedVersion">
    /// The version the app reports about itself. Anything unparseable is treated as
    /// <see cref="MobileVersionVerdict.UpdateRequired"/> once a minimum is configured: the server cannot
    /// establish that such a build is supported, and saying so is safe because the client only ever
    /// blocks on a verdict it actually received (see info/orbit-maui-plan.md's "Forced update").
    /// </param>
    public MobileVersionVerdict Decide(string? reportedVersion)
    {
        if (ParseOrNull(reportedVersion) is not { } version)
        {
            // Unreadable, and there is a minimum to fall short of - so the server cannot establish that
            // this build is supported. With no minimum configured there is nothing to fail against.
            return MinimumSupported is null ? MobileVersionVerdict.Supported : MobileVersionVerdict.UpdateRequired;
        }

        if (MinimumSupported is not null && version < MinimumSupported)
        {
            return MobileVersionVerdict.UpdateRequired;
        }

        return Latest is not null && version < Latest
            ? MobileVersionVerdict.UpdateAvailable
            : MobileVersionVerdict.Supported;
    }

    private static Version? ParseOrNull(string? value)
        => Version.TryParse(value, out var parsed) ? parsed : null;
}
