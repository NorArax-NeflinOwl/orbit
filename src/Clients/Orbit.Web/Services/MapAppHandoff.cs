namespace Orbit.Web.Services;

/// <summary>
/// Whether pressing a shared position on the map screen should hand it to the device's own map app
/// instead of centring Orbit's map on it - see mapApp.js, which does the handing.
///
/// The button is always there; this is only about what the row *itself* does, which is a question about
/// whether Orbit's map can answer at all. Two ways it cannot, and both are ordinary on a phone:
/// the tiles are withheld because the reader keeps third parties out (see mapTiles.js), so the map is a
/// blank square with pins on it; or Orbit has never been told where the reader is, so it can show the
/// pin and nothing about how far away it is. In either case centring is a gesture that answers nothing,
/// while the map app knows where its owner stands and how to get them there.
///
/// Only on a phone. On a desktop there is usually no map app to open, the map is big enough to read, and
/// a press that navigated away from the page would be a surprise.
/// </summary>
public static class MapAppHandoff
{
    public static bool ShouldOpenTheMapApp(bool isPhone, bool mapTilesAllowed, bool knowsWhereTheReaderIs)
        => isPhone && (!mapTilesAllowed || !knowsWhereTheReaderIs);
}
