using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Mobile.Authentication;
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
        MobileVersionGate versionGate, SessionStore sessionStore, IScreenNavigator navigator, IUpdateLink updateLink)
    {
        _versionGate = versionGate;
        _sessionStore = sessionStore;
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

    public async Task ContinueToAppAsync()
    {
        if (await _sessionStore.GetAsync() is null)
        {
            _navigator.ShowSignIn();
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
