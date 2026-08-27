using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Mobile.Authentication;
using Orbit.Mobile.Notifications;
using Orbit.Mobile.Update;

namespace Orbit.Mobile.Screens.Startup;

/// <summary>
/// The first thing that runs. Asks the version gate whether this build may run at all, and only then
/// decides between the sign-in screen and the app - see info/orbit-maui-plan.md's "Forced update".
/// </summary>
public sealed partial class StartupViewModel : ObservableObject
{
    private readonly MobileVersionGate _versionGate;
    private readonly SessionStore _sessionStore;
    private readonly NotificationOpener _notificationOpener;
    private readonly IScreenNavigator _navigator;
    private readonly IUpdateLink _updateLink;

    [ObservableProperty]
    private bool _isChecking = true;

    [ObservableProperty]
    private bool _isBlocked;

    [ObservableProperty]
    private string _blockedMessage = string.Empty;

    [ObservableProperty]
    private bool _canOpenUpdate;

    private string? _updateUrl;

    public StartupViewModel(
        MobileVersionGate versionGate, SessionStore sessionStore, NotificationOpener notificationOpener,
        IScreenNavigator navigator, IUpdateLink updateLink)
    {
        _versionGate = versionGate;
        _sessionStore = sessionStore;
        _notificationOpener = notificationOpener;
        _navigator = navigator;
        _updateLink = updateLink;
    }

    /// <summary>
    /// The verdict, having already put the screen into its blocked state if there is one. The caller
    /// handles the dismissible case, which needs a prompt only a page can show.
    /// </summary>
    public async Task<VersionGateDecision> DecideAsync()
    {
        var decision = await _versionGate.DecideAsync();
        if (!decision.StopsTheApp)
        {
            return decision;
        }

        _updateUrl = decision.UpdateUrl;
        CanOpenUpdate = !string.IsNullOrEmpty(decision.UpdateUrl);
        BlockedMessage = decision.LatestVersion is { } latest
            ? $"This version of Orbit is no longer supported. Update to {latest} to continue."
            : "This version of Orbit is no longer supported. Update to continue.";
        IsBlocked = true;
        IsChecking = false;
        return decision;
    }

    /// <summary>
    /// Where the app opens. A notification the reader tapped to launch it wins over the usual landing
    /// screen - that tap is the most recent thing they asked for - but only once they are signed in, and
    /// only if it leads somewhere. Everything else falls through to Notes, so a launch always ends on a
    /// screen rather than on the splash.
    /// </summary>
    public async Task ContinueToAppAsync()
    {
        if (await _sessionStore.GetAsync() is null)
        {
            _navigator.ShowSignIn();
            return;
        }

        if (await _notificationOpener.FollowTapThatLaunchedTheAppAsync())
        {
            return;
        }

        _navigator.ShowNotes();
    }

    [RelayCommand]
    private async Task OpenUpdateAsync()
    {
        if (_updateUrl is { } url)
        {
            await _updateLink.OpenAsync(url);
        }
    }
}
