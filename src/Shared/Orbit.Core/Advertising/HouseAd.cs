namespace Orbit.Core.Advertising;

/// <summary>
/// One thing an advertising slot can show. <see cref="Title"/>, <see cref="Body"/> and
/// <see cref="ActionLabel"/> are English and go through each client's own translations, the same way
/// every other string in Orbit does - so a slot reads in the language the rest of the screen is in.
///
/// <see cref="Url"/> is a path on this Orbit, never an address somewhere else. That is the whole of what
/// makes these safe to show without asking anybody's permission first: nothing is fetched from a third
/// party, nothing about the reader leaves the browser, and the slot cannot become a way for somebody
/// else's script to run here. See <see cref="HouseAds"/> for what would change if a real advertising
/// network were ever put behind these slots.
/// </summary>
/// <param name="ShowsOnAPhone">
/// Whether it is worth showing in the app as well as in a browser. False for anything whose subject is
/// the app itself: "get Orbit on your phone", read on a phone that already has it, is the one advert
/// that makes its reader trust the rest of them less.
/// </param>
public sealed record HouseAd(
    string Title, string Body, string ActionLabel, string Url, bool ShowsOnAPhone = true);
