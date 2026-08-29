namespace Orbit.Mobile.Screens.Location;

/// <summary>
/// One thing to draw on the map. Deliberately not a MAUI type: the map control's own pin carries a
/// platform Location, and letting that into here would drag the app head into the layer that can be
/// tested. The page turns these into pins, which is presentation and nothing more.
/// </summary>
/// <param name="IsMine">
/// Whether this is the reader's own position rather than somebody else's, so the two can be told apart
/// on a map where every pin otherwise looks alike.
/// </param>
public sealed record MapPoint(string Label, string? Address, double Latitude, double Longitude, bool IsMine)
{
    /// <summary>What the pin says when tapped - the address if there is one, the numbers otherwise.</summary>
    public string Description => Address is { Length: > 0 } address ? address : $"{Latitude:F5}, {Longitude:F5}";
}
