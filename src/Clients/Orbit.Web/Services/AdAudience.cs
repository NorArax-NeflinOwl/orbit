using Orbit.Core.Permissions;

namespace Orbit.Web.Services;

/// <summary>
/// Whether this reader may be shown adverts at all - the one question every advertising surface asks
/// (AdSlot, AdDialog, and the pacing in AdInterruption), held in one place so that none of them
/// re-derives it and no two of them can disagree.
///
/// Yes for everybody, except an account holding the Debugger permission: that one is shown adverts only
/// once "Allow ads" is switched on under Options, Debugger (DevicePreferences.AllowAdsForDebugger). Off by
/// default, because whoever holds the permission is working on Orbit rather than reading it.
///
/// Scoped, like UserPermissionState which it reads: the answer changes when either the permissions are
/// read again or the switch is flipped, and <see cref="Changed"/> says so, so a slot already on screen can
/// take itself away without waiting for the next navigation.
/// </summary>
public sealed class AdAudience : IDisposable
{
    private readonly UserPermissionState _permissions;
    private readonly DevicePreferences _devicePreferences;

    public AdAudience(UserPermissionState permissions, DevicePreferences devicePreferences)
    {
        _permissions = permissions;
        _devicePreferences = devicePreferences;
        _permissions.Changed += OnChanged;
        _devicePreferences.Changed += OnChanged;
    }

    /// <summary>Raised when either half of the answer may have changed.</summary>
    public event Action? Changed;

    public bool MayShowAds
        => !_permissions.Has(ApplicationPermission.Debug) || _devicePreferences.AllowAdsForDebugger;

    private void OnChanged() => Changed?.Invoke();

    /// <summary>DevicePreferences is a singleton, so this scoped listener takes itself off it when its scope ends.</summary>
    public void Dispose()
    {
        _permissions.Changed -= OnChanged;
        _devicePreferences.Changed -= OnChanged;
    }
}
