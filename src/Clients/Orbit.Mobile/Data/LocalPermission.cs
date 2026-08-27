namespace Orbit.Mobile.Data;

/// <summary>
/// One part of Orbit this account has been unlocked for, remembered between launches.
///
/// The web re-reads this from the server before it decides anything and can afford to: it is never
/// offline. A phone is, and a cold start with no connection would otherwise hide chat and the map from
/// somebody who has both - so the last answer the server gave is kept and used until it gives another.
///
/// Presentation only, exactly as on the web: the refusal itself is the server's (see PermissionPolicies
/// in Orbit.Api). It lives in the database rather than in preferences so that emptying the store when
/// the phone stops being yours takes it with everything else - see <see cref="LocalStoreReset"/>.
/// </summary>
public sealed class LocalPermission
{
    /// <summary>The <c>Orbit.Core.Permissions.ApplicationPermission</c> name, as the server sends it.</summary>
    public string Name { get; set; } = string.Empty;
}
