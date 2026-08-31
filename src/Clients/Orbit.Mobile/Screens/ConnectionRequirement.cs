using CommunityToolkit.Mvvm.ComponentModel;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Sync;

namespace Orbit.Mobile.Screens;

/// <summary>
/// Whether the things that only work with a connection can be offered right now.
///
/// Most of this app works offline; a handful of actions cannot, because each needs a verdict only the
/// server can give - is this username free, does this person exist, may this be shared. Until now those
/// said so *after* being tapped, which is the wrong order: somebody types a new password, presses the
/// button and only then learns it was never going to work.
///
/// So the screens ask this instead, and disable what cannot be done while explaining why. One object
/// rather than a bool per screen, because the explanation has to read the same everywhere and because
/// the answer changes while a screen is open - a phone that finds a network mid-form must not leave the
/// button dead, and nobody thinks to leave the screen and come back.
///
/// It says nothing about whether a request will succeed. A connected phone on a captive portal reaches
/// nothing, which is why every one of those actions still handles its own failure - see INetworkStatus.
/// </summary>
public sealed partial class ConnectionRequirement : ObservableObject
{
    private readonly Translations _translations;

    public ConnectionRequirement(INetworkStatus networkStatus, Translations translations)
    {
        _translations = translations;
        IsMet = networkStatus.IsOnline;
        networkStatus.Changed += (_, _) => IsMet = networkStatus.IsOnline;
    }

    /// <summary>True while the actions that need a connection can be offered.</summary>
    [ObservableProperty]
    private bool _isMet;

    public bool IsNotMet => !IsMet;

    /// <summary>
    /// What to say beside whatever has been disabled. Deliberately about the connection rather than
    /// about the action: the reader is not being refused, they are being asked to wait.
    /// </summary>
    public string Explanation => _translations["This needs a connection. It will work again once you're back online."];

    partial void OnIsMetChanged(bool value) => OnPropertyChanged(nameof(IsNotMet));
}
