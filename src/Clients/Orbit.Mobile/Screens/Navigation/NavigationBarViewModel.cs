using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Mobile.Api;
using Orbit.Mobile.Authentication;

namespace Orbit.Mobile.Screens.Navigation;

/// <summary>
/// The bar across the top of every signed-in screen: the way to each section, and who is signed in.
///
/// Mirrors what Orbit.Web shows on a narrow window, where the sidebar becomes exactly this - a logo
/// standing in for the Dashboard link, the section icons without their labels, and the avatar pushed to
/// the far right (see app.css's 680px breakpoint). Matching it is the point: somebody who uses both
/// should not have to learn the app twice.
///
/// One view model per bar rather than one shared instance. The bar is part of each page, and a page
/// that is not on screen has no business refreshing an unread count.
/// </summary>
public sealed partial class NavigationBarViewModel : ObservableObject
{
    private readonly SessionStore _sessionStore;
    private readonly NotificationsClient _notificationsClient;
    private readonly IScreenNavigator _navigator;

    /// <summary>The signed-in reader's initials, which is what the avatar shows - there are no pictures in Orbit.</summary>
    [ObservableProperty]
    private string _initials = string.Empty;

    [ObservableProperty]
    private string _unreadLabel = string.Empty;

    public NavigationBarViewModel(
        SessionStore sessionStore, NotificationsClient notificationsClient, IScreenNavigator navigator)
    {
        _sessionStore = sessionStore;
        _notificationsClient = notificationsClient;
        _navigator = navigator;
    }

    public bool HasUnread => UnreadLabel.Length > 0;

    partial void OnUnreadLabelChanged(string value) => OnPropertyChanged(nameof(HasUnread));

    /// <summary>
    /// Fills in who is signed in, then asks about unread notifications. The initials come from the
    /// stored session so the bar is complete offline; only the badge needs the server, and its absence
    /// is not worth a message - a missing badge reads as "nothing new", which is the safer wrong answer.
    /// </summary>
    [RelayCommand]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        Initials = InitialsOf((await _sessionStore.GetAsync())?.DisplayName);

        try
        {
            UnreadLabel = FormatCount((await _notificationsClient.GetUnreadAsync(cancellationToken)).Count);
        }
        catch (HttpRequestException)
        {
        }
        catch (OperationCanceledException)
        {
        }
    }

    [RelayCommand]
    private void GoToDashboard() => _navigator.ShowDashboard();

    [RelayCommand]
    private void GoToNotes() => _navigator.ShowNotes();

    [RelayCommand]
    private void GoToTasks() => _navigator.ShowTasks();

    [RelayCommand]
    private void GoToCalendar() => _navigator.ShowCalendar();

    [RelayCommand]
    private void GoToInventory() => _navigator.ShowInventory();

    [RelayCommand]
    private void GoToMap() => _navigator.ShowMap();

    [RelayCommand]
    private void GoToContacts() => _navigator.ShowContacts();

    /// <summary>
    /// The avatar leads to the account, which is where the web's avatar dropdown puts the same things -
    /// options, notifications and signing out. A phone has no room for a hovering menu.
    /// </summary>
    [RelayCommand]
    private void GoToAccount() => _navigator.ShowAccount();

    /// <summary>
    /// Two initials at most, from the first and last word of the display name. Falls back to the first
    /// letter for a one-word name, and to nothing at all rather than a placeholder glyph for an empty
    /// one - an avatar reading "?" looks like a fault rather than an unnamed account.
    /// </summary>
    private static string InitialsOf(string? displayName)
    {
        var words = (displayName ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return words switch
        {
            [] => string.Empty,
            [var only] => only[..1].ToUpperInvariant(),
            [var first, .., var last] => $"{first[..1]}{last[..1]}".ToUpperInvariant()
        };
    }

    /// <summary>
    /// Capped the way the web caps it: past a certain point the exact number stops being information
    /// and starts being a number too wide for the badge.
    /// </summary>
    private static string FormatCount(int unread) => unread switch
    {
        <= 0 => string.Empty,
        > 99 => "99+",
        _ => unread.ToString()
    };
}
