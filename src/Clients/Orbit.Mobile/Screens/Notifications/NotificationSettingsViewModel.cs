using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Contracts.Notifications;
using Orbit.Mobile.Api;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens;

namespace Orbit.Mobile.Screens.Notifications;

/// <summary>
/// The switches deciding what Orbit is allowed to interrupt the reader with.
///
/// These are account-wide, not per-device: the same row the web's Options page edits. That makes the
/// save the delicate part rather than the screen - the endpoint replaces the whole settings object, and
/// this screen deliberately shows only the switches that mean something on a phone. The ones it does
/// not show are carried through untouched from what was loaded, so saving here cannot quietly undo a
/// choice made in a browser. See <see cref="SaveAsync"/>.
/// </summary>
public sealed partial class NotificationSettingsViewModel : ObservableObject
{
    private readonly NotificationsClient _notificationsClient;
    private readonly Translations _translations;
    private readonly IScreenNavigator _navigator;

    /// <summary>
    /// What the server last told us, kept whole so the switches this screen does not offer survive a
    /// save. Null until the first load succeeds, which is what stops a save from inventing them.
    /// </summary>
    private NotificationSettingsDto? _loaded;

    [ObservableProperty]
    private string _message = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    /// <summary>The master switch: off means nothing is recorded or delivered at all.</summary>
    [ObservableProperty]
    private bool _allowNotifications;

    [ObservableProperty]
    private bool _allowPush;

    [ObservableProperty]
    private bool _allowEmail;

    /// <summary>Off by default on the server, and worth surfacing because being shared something is the one case that stays quiet unless asked for.</summary>
    [ObservableProperty]
    private bool _allowShareNotifications;

    public NotificationSettingsViewModel(
        NotificationsClient notificationsClient, Translations translations, IScreenNavigator navigator)
    {
        _notificationsClient = notificationsClient;
        _translations = translations;
        _navigator = navigator;
    }

    public bool HasMessage => Message.Length > 0;

    /// <summary>
    /// Nothing below the master switch can do anything while it is off, so the screen says so rather
    /// than offering switches that have no effect.
    /// </summary>
    public bool CanChooseChannels => AllowNotifications;

    /// <summary>False until a load has succeeded: saving what was never read would write guesses.</summary>
    public bool CanSave => _loaded is not null && !IsBusy;

    partial void OnMessageChanged(string value) => OnPropertyChanged(nameof(HasMessage));

    /// <summary>
    /// Tells the command, not just the binding. Show() runs inside the load's try, so it asks whether
    /// saving is possible while IsBusy is still true and gets "no"; without re-asking here, when the
    /// finally clears IsBusy, the Save button stayed disabled for the life of the screen and notification
    /// settings could be read but never changed.
    /// </summary>
    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanSave));
        SaveCommand.NotifyCanExecuteChanged();
    }

    partial void OnAllowNotificationsChanged(bool value) => OnPropertyChanged(nameof(CanChooseChannels));

    [RelayCommand]
    private void GoBack() => _navigator.ShowNotifications();

    [RelayCommand]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;
        try
        {
            Show(await _notificationsClient.GetSettingsAsync(cancellationToken));
            Message = string.Empty;
        }
        catch (HttpRequestException exception)
        {
            Message = Explain(exception, "Couldn't read your notification settings");
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        if (_loaded is not { } current)
        {
            return;
        }

        IsBusy = true;
        try
        {
            // Everything this screen does not show comes from what was loaded, not from a default. A
            // banner duration tuned in the browser is not this screen's business to reset.
            Show(await _notificationsClient.SaveSettingsAsync(
                new UpdateNotificationSettingsRequest(
                    AllowNotifications, AllowPush, AllowEmail, current.AllowMobileBanner,
                    current.ShowExceptionDetails, current.BannerVisibleSeconds, current.BannerMinimumGapSeconds,
                    AllowShareNotifications, current.RetentionDays),
                cancellationToken));

            Message = _translations["Saved."];
        }
        catch (HttpRequestException exception)
        {
            Message = Explain(exception, "Couldn't save your notification settings");
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Show(NotificationSettingsDto settings)
    {
        _loaded = settings;
        AllowNotifications = settings.AllowNotifications;
        AllowPush = settings.AllowPush;
        AllowEmail = settings.AllowEmail;
        AllowShareNotifications = settings.AllowShareNotifications;
        OnPropertyChanged(nameof(CanSave));
        SaveCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Reached-and-refused is not the same as unreachable: a null status is the only thing that means
    /// the request never landed, and telling somebody they are offline when the server answered sends
    /// them looking in the wrong place.
    /// </summary>
    /// <param name="what">A dictionary key naming the thing that failed, not the text itself.</param>
    private string Explain(HttpRequestException exception, string what)
        => exception.StatusCode is null
            ? _translations.Format("{0} - Orbit is out of reach.", _translations[what])
            : _translations.Format("{0}. Try signing in again.", _translations[what]);
}
