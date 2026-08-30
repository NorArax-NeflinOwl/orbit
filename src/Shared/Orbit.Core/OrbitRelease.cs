namespace Orbit.Core;

/// <summary>
/// What Orbit says about itself: which build this is, when it was made, and under what licence.
///
/// One place because it is shown in several - the web client's footer and the phone's About entry - and
/// three copies of a version number is three chances for the one somebody reads to be wrong. The phone
/// head's ApplicationDisplayVersion has to match <see cref="Version"/>: it is the value the version gate
/// compares against, so a build that disagrees with this is a build that reports itself wrongly to the
/// server (see Orbit.Core.Mobile.MobileVersionPolicy).
/// </summary>
public static class OrbitRelease
{
    /// <summary>SemVer, matching Orbit.Maui's ApplicationDisplayVersion.</summary>
    public const string Version = "0.1.0";

    /// <summary>
    /// The year this build was made, for the copyright line. A constant rather than DateTime.Now.Year:
    /// a footer that silently rolls over on New Year's Eve is claiming a build was made in a year it
    /// was not, and the whole point of the line is to say when this one was.
    /// </summary>
    public const int Year = 2026;

    public const string Author = "Patryk Pudwel";

    /// <summary>What the LICENSE file at the root of the repository says, in three words.</summary>
    public const string LicenseName = "All Rights Reserved";

    /// <summary>Where that file can be read.</summary>
    public const string LicenseUrl = "https://github.com/NorArax-NeflinOwl/orbit/blob/main/LICENSE";

    /// <summary>The copyright line itself, assembled once so both clients say it identically.</summary>
    public static string Copyright => $"© {Year} {Author}";
}
