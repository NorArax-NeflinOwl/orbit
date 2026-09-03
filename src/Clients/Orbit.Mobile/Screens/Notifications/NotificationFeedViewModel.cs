using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Mobile.Api;
using Orbit.Mobile.Data;
using Orbit.Mobile.Sync;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Notifications;
using Orbit.Mobile.Screens;

namespace Orbit.Mobile.Screens.Notifications;

/// <summary>
/// The in-app notification feed: what happened while the reader was elsewhere, and a way back to each
/// of them.
///
/// Reads from this phone's own copy, like every other screen. It was the last one that did not: the
/// feed was fetched from the server each time, so with no connection it was simply empty - an overdue
/// task nobody could see on a train. It now syncs and then reads what it holds.
///
/// The two actions on it - reading, clearing - are still the server's, because they are about the
/// account rather than about this phone, and the second device has to hear about them. They are
/// disabled with no connection rather than offered and refused - see ConnectionRequirement.
/// </summary>
public sealed partial class NotificationFeedViewModel : ObservableObject
{
    private readonly NotificationsClient _notificationsClient;
    private readonly LocalNotificationRepository _notifications;
    private readonly NotificationSynchronizer _synchronizer;
    private readonly NotificationOpener _opener;
    private readonly Translations _translations;
    private readonly IScreenNavigator _navigator;

    [ObservableProperty]
    private string _message = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    /// <summary>
    /// Whether the feed is showing everything held rather than just the recent entries. The cleared ones
    /// only appear here, which is the point of having the switch at all.
    /// </summary>
    [ObservableProperty]
    private bool _isShowingEverything;

    public NotificationFeedViewModel(
        NotificationsClient notificationsClient, LocalNotificationRepository notifications,
        NotificationSynchronizer synchronizer, NotificationOpener opener, Translations translations,
        IScreenNavigator navigator, ConnectionRequirement connection, Live.ILiveUpdates liveUpdates)
    {
        // Read again when something says there is something to read, rather than only when this screen
        // is opened - see ILiveUpdates.
        liveUpdates.NotificationsChanged += () => _ = ShowFeedAsync(CancellationToken.None);
        _notificationsClient = notificationsClient;
        _notifications = notifications;
        _synchronizer = synchronizer;
        Connection = connection;
        _translations = translations;
        _opener = opener;
        _navigator = navigator;
    }

    public ObservableCollection<NotificationRow> Rows { get; } = [];

    /// <summary>Reading and clearing are the server's to record, so they are not offered without it.</summary>
    public ConnectionRequirement Connection { get; }

    public bool HasMessage => Message.Length > 0;

    public bool HasNothing => Rows.Count == 0 && !IsBusy;

    /// <summary>What the switch offers next, rather than what it is showing now - it is a button, not a label.</summary>
    public string ShowEverythingLabel => IsShowingEverything ? _translations["Recent only"] : _translations["Show all"];

    partial void OnMessageChanged(string value) => OnPropertyChanged(nameof(HasMessage));

    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(HasNothing));

    partial void OnIsShowingEverythingChanged(bool value) => OnPropertyChanged(nameof(ShowEverythingLabel));

    [RelayCommand]
    private Task LoadAsync(CancellationToken cancellationToken) => ShowFeedAsync(cancellationToken);


    [RelayCommand]
    private void GoBack() => _navigator.ShowDashboard();

    /// <summary>
    /// Marks the whole feed read. Deliberately separate from clearing: read means "I have seen these",
    /// cleared means "take them out of my way", and the server keeps them apart too.
    /// </summary>
    [RelayCommand]
    private async Task MarkEverythingReadAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Both sides: the server does not know about what this phone raised for itself, and left to
            // the server alone "mark all read" would leave one stubbornly unread - see
            // LocalNotificationRepository.MarkEverythingReadAsync.
            await _notifications.MarkEverythingReadAsync(cancellationToken);
            await _notificationsClient.MarkAllReadAsync(cancellationToken);
            await ShowFeedAsync(cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            Message = Explain(exception, _translations["Couldn't mark them read"]);
        }
        catch (OperationCanceledException)
        {
        }
    }

    [RelayCommand]
    private async Task ClearAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _notifications.DismissEverythingAsync(cancellationToken);
            await _notificationsClient.ClearAsync(cancellationToken);
            await ShowFeedAsync(cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            Message = Explain(exception, _translations["Couldn't clear them"]);
        }
        catch (OperationCanceledException)
        {
        }
    }

    [RelayCommand]
    private async Task ShowEverythingAsync(CancellationToken cancellationToken)
    {
        IsShowingEverything = !IsShowingEverything;
        await ShowFeedAsync(cancellationToken);
    }

    /// <summary>
    /// Opens what a notification was about. Marking it read happens on the way rather than on arrival:
    /// the reader has plainly seen it by the time they tap it, and the screens they land on cannot all
    /// be relied on to report back.
    /// </summary>
    [RelayCommand]
    private async Task OpenAsync(NotificationRow? row)
    {
        if (row is null)
        {
            return;
        }

        try
        {
            var outcome = await _opener.OpenAsync(row.Url);
            Message = outcome switch
            {
                NotificationOpenOutcome.NowhereToGo => _translations[
                    "This notification points somewhere this version of Orbit doesn't have. Updating should fix it."],
                NotificationOpenOutcome.NotOnThisPhoneYet => _translations[
                    "Couldn't find what this is about on this phone. It may need a connection to catch up first."],
                _ => string.Empty
            };

            if (outcome == NotificationOpenOutcome.Opened)
            {
                await _notifications.MarkReadAsync(row.Id);
                if (row.Url is { Length: > 0 } url)
                {
                    await _notificationsClient.MarkReadAtAsync(url);
                }
            }
        }
        catch (HttpRequestException)
        {
            // The reader is where they wanted to be; failing to record that is not worth a message.
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task ShowFeedAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;
        try
        {
            // Asked for first and read afterwards, so a phone with a connection shows what the server
            // has and one without shows what it heard last - rather than nothing at all.
            await _synchronizer.SynchroniseAsync(cancellationToken);

            var entries = IsShowingEverything
                ? await _notifications.GetHistoryAsync(cancellationToken)
                : await _notifications.GetRecentAsync(cancellationToken);

            Rows.Clear();
            foreach (var entry in entries)
            {
                Rows.Add(new NotificationRow(entry, _translations));
            }

            Message = string.Empty;
        }
        // Being out of reach never lands here - the synchroniser answers "never got through" and the
        // feed shows what it holds. What does land here is a refusal, most often an expired session,
        // and that still has to be said: it is not something waiting for a connection will fix.
        catch (HttpRequestException exception)
        {
            Message = Explain(exception, _translations["Couldn't read your notifications"]);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(HasNothing));
        }
    }

    /// <summary>
    /// Reached-and-refused is not the same as unreachable: a null status is the only thing that means
    /// the request never landed, and telling somebody they are offline when the server answered sends
    /// them looking in the wrong place.
    /// </summary>
    /// <param name="what">Already translated - see the call sites, which ask the dictionary themselves.</param>
    private string Explain(HttpRequestException exception, string what)
        => exception.StatusCode is null
            ? _translations.Format("{0} - Orbit is out of reach.", what)
            : _translations.Format("{0}. Try signing in again.", what);
}
