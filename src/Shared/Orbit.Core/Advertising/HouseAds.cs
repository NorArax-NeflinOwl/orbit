namespace Orbit.Core.Advertising;

/// <summary>
/// What the advertising slots show: Orbit's own things, written here and served by Orbit itself.
///
/// **No third party is involved, and that is deliberate rather than temporary.** Orbit already asks
/// before it lets a single outside request happen - the map's tiles are withheld from a reader who has
/// said not to share their information (see mapTiles.js), which is the one third-party request in the
/// whole app. A real advertising network is that same decision several times over: a script from
/// somebody else's domain, running in the reader's browser, told who they are by being there at all.
/// Putting one behind these slots is therefore its own piece of work with its own consent question, an
/// account somebody has to open, and keys to keep - see info/future-plan.md. What is here is the slot,
/// the frame around it, and the rule about who sees it, which is everything that has to exist first.
///
/// The order is the order they were written in, and a slot picks by index rather than at random: the
/// same page drawn twice in a row shows the same thing, which is what stops a slot flickering through
/// its list every time anything on the page changes.
/// </summary>
public static class HouseAds
{
    public static IReadOnlyList<HouseAd> All { get; } =
    [
        new("Orbit on your phone",
            "The same notes, lists and calendar, offline and in your pocket.",
            "Get the app",
            "/download",
            ShowsOnAPhone: false),
        new("Everything Orbit can do",
            "The parts you have not unlocked yet, and what each of them is for.",
            "Read the docs",
            "/docs"),
        new("Nothing here is read by anybody else",
            "What Orbit keeps, what it sends, and what it seals so even the server cannot open it.",
            "How that works",
            "/security")
    ];

    /// <summary>Everything worth showing inside the app - see <see cref="HouseAd.ShowsOnAPhone"/>.</summary>
    public static IReadOnlyList<HouseAd> OnAPhone { get; } = [.. All.Where(advert => advert.ShowsOnAPhone)];

    /// <summary>
    /// The advert for a slot, chosen from the number the caller has - a page's own counter, or the
    /// number of times a slot has been drawn. Wraps, so any number is an answer; on an empty list there
    /// is nothing to show and this says so with null rather than throwing at draw time.
    /// </summary>
    public static HouseAd? ForSlot(int slot) => From(All, slot);

    /// <summary>The same, out of what is worth showing inside the app.</summary>
    public static HouseAd? ForSlotOnAPhone(int slot) => From(OnAPhone, slot);

    private static HouseAd? From(IReadOnlyList<HouseAd> adverts, int slot)
        => adverts.Count == 0 ? null : adverts[(int)((uint)slot % (uint)adverts.Count)];
}
