namespace Orbit.Mobile.Screens.About;

/// <summary>
/// Where the documents Orbit publishes about itself can be read.
///
/// Four of the five are pages of the web client - it is the same deployment and the same words, and a
/// second copy of a privacy statement is a second chance for the one somebody reads to be out of date.
/// The phone has no address for them of its own, so the app head supplies this from what its build was
/// told (see OrbitWebSettings in Orbit.Maui); a build told nothing has null for each and the About
/// screen leaves that row out rather than offering a link that goes nowhere.
///
/// The licence is the exception and is never null: it is a file in the repository, at an address that
/// is the same for every deployment - see <see cref="Orbit.Core.OrbitRelease.LicenseUrl"/>.
/// </summary>
public sealed record OrbitDocumentLinks(
    string? Privacy, string? Security, string? Documentation, string? DoNotShare)
{
    /// <summary>What a build that was told no web address has: the licence, and nothing else.</summary>
    public static readonly OrbitDocumentLinks NoneButTheLicence = new(null, null, null, null);

    /// <summary>
    /// The four pages under one origin, which is the ordinary case - every deployment serves them all
    /// from the client's own host, at the addresses Orbit.Web routes them at.
    /// </summary>
    public static OrbitDocumentLinks Under(Uri webBaseAddress)
        => new(
            new Uri(webBaseAddress, "privacy").AbsoluteUri,
            new Uri(webBaseAddress, "security").AbsoluteUri,
            new Uri(webBaseAddress, "docs").AbsoluteUri,
            // No page of its own on the web: it is a dialog opened from the footer, and the address
            // that opens it is the client's own front page. The switch behind it is stored on the
            // account, so answering it in a browser answers it for the phone as well.
            webBaseAddress.AbsoluteUri);
}
