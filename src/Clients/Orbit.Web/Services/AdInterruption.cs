using Orbit.Core.Permissions;

namespace Orbit.Web.Services;

/// <summary>
/// Whether the advert that interrupts is shown at all. One rule, in one place, because the answer is
/// the only thing about that advert worth arguing over - MainLayout does the showing.
///
/// Two conditions, and both are about not being a nuisance. It is shown once a visit, never again on
/// the next navigation: an advert that comes back on every page is the thing that makes people leave.
/// And it is never shown to an account holding the Debugger permission - whoever holds that is looking
/// at Orbit's own internals, which means they are working on it rather than reading it, and an advert
/// over the top of that interrupts without having anything to offer.
///
/// The slots beside the page are not gated on any of this: they sit where they are and wait, which is
/// what makes them the polite half of the same idea.
/// </summary>
public static class AdInterruption
{
    public static bool ShouldShow(UserPermissionState permissions, bool hasShownItThisVisit)
        => !hasShownItThisVisit && !permissions.Has(ApplicationPermission.Debug);
}
