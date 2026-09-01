namespace Orbit.Core;

/// <summary>
/// What Orbit says about itself: which build this is, when it was made, and under what licence.
///
/// One place because it is shown in several - the web client's footer and the phone's About entry - and
/// two copies of a copyright line is two chances for the one somebody reads to be wrong.
///
/// The version is deliberately not here: it is different per project and decided by the build, not by
/// anybody editing a file - see <see cref="OrbitVersion"/>.
/// </summary>
public static class OrbitRelease
{
    /// <summary>
    /// The year this build was made, for the copyright line. A constant rather than DateTime.Now.Year:
    /// a footer that silently rolls over on New Year's Eve is claiming a build was made in a year it
    /// was not, and the whole point of the line is to say when this one was.
    /// </summary>
    public const int Year = 2026;

    /// <summary>
    /// Who the copyright line names. The application's own name for now, standing in for the company's
    /// - a footer is a statement about who publishes this, and a person's name in that place says
    /// something narrower than what is meant. The LICENSE file still names the copyright holder, which
    /// is a legal question rather than a question about a footer.
    /// </summary>
    public const string PublishedBy = "Orbit";

    /// <summary>What the LICENSE file at the root of the repository says, in three words.</summary>
    public const string LicenseName = "All Rights Reserved";

    /// <summary>Where that file can be read.</summary>
    public const string LicenseUrl = "https://github.com/NorArax-NeflinOwl/orbit/blob/main/LICENSE";

    /// <summary>The copyright line itself, assembled once so both clients say it identically.</summary>
    public static string Copyright => $"© {Year} {PublishedBy}";
}
