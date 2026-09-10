using Orbit.Core.Permissions;

namespace Orbit.Web.Services;

/// <summary>
/// Whether the advert that interrupts is shown at all. One rule, in one place, because the answer is
/// the only thing about that advert worth arguing over - MainLayout does the showing.
///
/// Two conditions, and both are about not being a nuisance. <b>It is shown at most once every
/// <see cref="MinimumGap"/></b>, and the clock behind that is kept on the device rather than in the
/// page (see <see cref="LastAdInterruption"/>): it used to be a field that lasted as long as the page
/// did, so every refresh was a fresh visit and every refresh brought the advert back. And it is never
/// shown to an account holding the Debugger permission - whoever holds that is looking at Orbit's own
/// internals, which means they are working on it rather than reading it, and an advert over the top of
/// that interrupts without having anything to offer.
///
/// The slots beside the page are not gated on any of this: they sit where they are and wait, which is
/// what makes them the polite half of the same idea.
/// </summary>
public static class AdInterruption
{
    /// <summary>
    /// How long the reader is left alone between interruptions. The same shape the notification banner
    /// already had for the same problem - see NotificationSettings.BannerTiming - because "how often at
    /// most" is the only honest way to pace something that covers what somebody is reading.
    /// </summary>
    public static readonly TimeSpan MinimumGap = TimeSpan.FromMinutes(5);

    /// <param name="lastShownAtUtc">
    /// When this browser last showed one, or null for one that never has - which is also what a browser
    /// that has not been allowed to remember answers. See <see cref="LastAdInterruption"/>.
    /// </param>
    public static bool ShouldShow(
        UserPermissionState permissions, DateTimeOffset? lastShownAtUtc, DateTimeOffset nowUtc)
        => !permissions.Has(ApplicationPermission.Debug)
        && (lastShownAtUtc is not { } lastShown || nowUtc - lastShown >= MinimumGap);
}
