using Orbit.Web.Components;

namespace Orbit.Web.Services;

/// <summary>
/// A place somebody picked on the map, held for the one page they are being taken to next.
///
/// Carried here rather than in the address bar. The alternative would be
/// "/calendar/new?lat=52.2&amp;lon=21.0", which writes where somebody is going into their browser
/// history and into anything that later reads a URL - and a place is exactly the kind of thing that
/// should not be sitting in a link somebody might paste. Nothing about this needs to survive a reload:
/// it is a handover between two screens, a second apart.
///
/// Taken rather than read, for the same reason: the page it was meant for picks it up once, and coming
/// back to that page later must not silently fill the box again with somewhere you looked at yesterday.
/// </summary>
public sealed class ChosenPlace
{
    private PickedPlace? _waiting;

    /// <summary>Whether a place is waiting to be picked up - without taking it.</summary>
    public bool IsWaiting => _waiting is not null;

    public void Hold(PickedPlace place) => _waiting = place;

    /// <summary>Hands over the place and forgets it, so it is only ever used once.</summary>
    public PickedPlace? Take()
    {
        var place = _waiting;
        _waiting = null;
        return place;
    }
}
