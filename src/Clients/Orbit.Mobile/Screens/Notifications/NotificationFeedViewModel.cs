using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Mobile.Api;
using Orbit.Mobile.Notifications;
using Orbit.Mobile.Screens;

namespace Orbit.Mobile.Screens.Notifications;

/// <summary>
/// The in-app notification feed: what happened while the reader was elsewhere, and a way back to each
/// of them.
///
/// Needs a connection, unlike most screens here. The feed lives on the server - it is the same feed the
/// web shows - and every action on it (reading, clearing) is a server action. Caching it locally would
/// buy a list that cannot be acted on and would go stale silently, which is worse than saying so.
/// </summary>
public sealed partial class NotificationFeedViewModel : ObservableObject
{
    private readonly NotificationsClient _notificationsClient;
    private readonly NotificationOpener _opener;
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
        NotificationsClient notificationsClient, NotificationOpener opener, IScreenNavigator navigator)
    {
        _notificationsClient = notificationsClient;
        _opener = opener;
        _navigator = navigator;
    }

    public ObservableCollection<NotificationRow> Rows { get; } = [];

    public bool HasMessage => Message.Length > 0;

    public bool HasNothing => Rows.Count == 0 && !IsBusy;

    /// <summary>What the switch offers next, rather than what it is showing now - it is a button, not a label.</summary>
    public string ShowEverythingLabel => IsShowingEverything ? "Recent only" : "Show all";

    partial void OnMessageChanged(string value) => OnPropertyChanged(nameof(HasMessage));

    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(HasNothing));

    partial void OnIsShowingEverythingChanged(bool value) => OnPropertyChanged(nameof(ShowEverythingLabel));

    [RelayCommand]
    private Task LoadAsync(CancellationToken cancellationToken) => ShowFeedAsync(cancellationToken);

    [RelayCommand]
    private void GoToSettings() => _navigator.ShowNotificationSettings();

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
            await _notificationsClient.MarkAllReadAsync(cancellationToken);
            await ShowFeedAsync(cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            Message = Explain(exception, "Couldn't mark them read");
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
            await _notificationsClient.ClearAsync(cancellationToken);
            await ShowFeedAsync(cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            Message = Explain(exception, "Couldn't clear them");
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
                NotificationOpenOutcome.NowhereToGo =>
                    "This notification points somewhere this version of Orbit doesn't have. Updating should fix it.",
                NotificationOpenOutcome.NotOnThisPhoneYet =>
                    "Couldn't find what this is about on this phone. It may need a connection to catch up first.",
                _ => string.Empty
            };

            if (outcome == NotificationOpenOutcome.Opened && row.Url is { Length: > 0 } url)
            {
                await _notificationsClient.MarkReadAtAsync(url);
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
            var entries = IsShowingEverything
                ? await _notificationsClient.GetHistoryAsync(cancellationToken)
                : await _notificationsClient.GetRecentAsync(cancellationToken);

            Rows.Clear();
            foreach (var entry in entries)
            {
                Rows.Add(new NotificationRow(entry));
            }

            Message = string.Empty;
        }
        catch (HttpRequestException exception)
        {
            Message = Explain(exception, "Couldn't read your notifications");
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
    private static string Explain(HttpRequestException exception, string what)
        => exception.StatusCode is null
            ? $"{what} - Orbit is out of reach."
            : $"{what}. Try signing in again.";
}
