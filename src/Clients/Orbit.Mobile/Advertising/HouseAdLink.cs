using Orbit.Core.Advertising;

namespace Orbit.Mobile.Advertising;

/// <summary>
/// Where pressing an advert goes on a phone.
///
/// A house advert names a path on Orbit's web client - "/docs", "/security" - because that is where the
/// pages it points at are; the app has no docs screen and no security screen of its own. So a press
/// opens the browser, and the address has to be built out of the deployment's own web address, which the
/// server holds (see ClientFlagsDto.WebAddress) and the phone asks for. The bar was not something to tap
/// at all until 2026-09-10, on the strength of the app not being told that address - which it has been
/// since public share links needed it.
///
/// Nothing is opened when the deployment has not said where its web client is, or when the phone could
/// not ask: a bar that opens nothing is exactly what it was before, and better than one that opens a
/// browser at an address built out of a guess.
/// </summary>
public static class HouseAdLink
{
    /// <summary>
    /// The address to open, or null when there is none worth opening. <paramref name="webAddress"/> is
    /// what <see cref="Api.PublicShareClient.WebAddressAsync"/> answered - empty for a deployment that
    /// has not said, and for a phone that could not reach the server to ask.
    ///
    /// The advert's own path is required to be a path, not an address: an advert is Orbit's own copy and
    /// always names one (see <see cref="HouseAd.Url"/>), and keeping the rule here means the day one
    /// stops being Orbit's own is the day this refuses it rather than the day the app opens somebody
    /// else's site on a press.
    /// </summary>
    public static string? For(HouseAd? advert, string webAddress)
    {
        if (advert is null
            || !advert.Url.StartsWith('/')
            || advert.Url.StartsWith("//", StringComparison.Ordinal)
            || !Uri.TryCreate(webAddress, UriKind.Absolute, out var web)
            || web.Scheme is not ("http" or "https"))
        {
            return null;
        }

        return $"{web.AbsoluteUri.TrimEnd('/')}{advert.Url}";
    }
}
