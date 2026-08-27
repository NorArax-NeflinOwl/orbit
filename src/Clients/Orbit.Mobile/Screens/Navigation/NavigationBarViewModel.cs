using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Mobile.Api;
using Orbit.Mobile.Authentication;
using Orbit.Mobile.Presence;

namespace Orbit.Mobile.Screens.Navigation;

/// <summary>
/// The bar across the top of every signed-in screen: the way to each section, and who is signed in.
///
/// Mirrors what Orbit.Web shows on a narrow window, where the sidebar becomes exactly this - a logo
/// standing in for the Dashboard link, the section icons without their labels, and the avatar pushed to
/// the far right (see app.css's 680px breakpoint). Matching it is the point: somebody who uses both
/// should not have to learn the app twice.
///
/// One shared instance rather than one per page: the bar and the menu the avatar opens are two
/// controls that have to agree about whether that menu is open, and only one page is ever on screen.
/// </summary>
public sealed partial class NavigationBarViewModel : ObservableObject
{
    private readonly SessionStore _sessionStore;
    private readonly NotificationsClient _notificationsClient;
    private readonly AuthenticationClient _authenticationClient;
    private readonly Presence.Presence _presence;
    private readonly IScreenNavigator _navigator;

    /// <summary>The signed-in reader's initials, which is what the avatar shows - there are no pictures in Orbit.</summary>
    [ObservableProperty]
    private string _initials = string.Empty;

    [ObservableProperty]
    private string _unreadLabel = string.Empty;

    [ObservableProperty]
    private PresenceAppearance _appearance = PresenceAppearance.Active;

    /// <summary>Whose account this is, shown at the top of the menu as the web's dropdown does.</summary>
    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private bool _isMenuOpen;

    /// <summary>
    /// Whether the status choices are showing. Collapsed by default: the menu's job is to list what is
    /// behind the avatar, and a row that is already unfolded pushes the rest of the list down to offer
    /// something most visits do not want.
    /// </summary>
    [ObservableProperty]
    private bool _isStatusExpanded;

    public NavigationBarViewModel(
        SessionStore sessionStore, NotificationsClient notificationsClient,
        AuthenticationClient authenticationClient, Presence.Presence presence, IScreenNavigator navigator)
    {
        _sessionStore = sessionStore;
        _notificationsClient = notificationsClient;
        _authenticationClient = authenticationClient;
        _presence = presence;
        _navigator = navigator;
        _presence.Changed += OnPresenceChanged;
        ShowPresence();
    }

    private void OnPresenceChanged(object? sender, EventArgs e) => ShowPresence();

    /// <summary>
    /// The dot in the avatar's top-right corner. Kept as the four states rather than a colour, so what
    /// the app decided stays testable and only the page turns it into a colour.
    /// </summary>
    private void ShowPresence() => Appearance = _presence.Appearance;

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
        _presence.MarkActive();
        DisplayName = (await _sessionStore.GetAsync())?.DisplayName ?? string.Empty;
        Initials = InitialsOf(DisplayName);

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
    /// The avatar opens a menu rather than going anywhere, the same as Orbit.Web's: the account, the
    /// notifications and signing out all hang off it, and making the avatar mean one of them would hide
    /// the other two.
    /// </summary>
    [RelayCommand]
    private void ToggleMenu()
    {
        _presence.MarkActive();
        IsMenuOpen = !IsMenuOpen;
        if (!IsMenuOpen)
        {
            IsStatusExpanded = false;
        }
    }

    [RelayCommand]
    private void CloseMenu()
    {
        IsMenuOpen = false;
        // Folded away with the menu, so the next visit opens on the list rather than mid-choice.
        IsStatusExpanded = false;
    }

    [RelayCommand]
    private void ToggleStatus() => IsStatusExpanded = !IsStatusExpanded;

    [RelayCommand]
    private void GoToAccount() => LeaveMenuFor(_navigator.ShowAccount);

    [RelayCommand]
    private void GoToNotifications() => LeaveMenuFor(_navigator.ShowNotifications);

    /// <summary>Whether the reader has chosen to be shown as available - see Presence for the other two states.</summary>
    public bool IsAvailable => _presence.Chosen == ChosenAvailability.Available;

    public bool IsUnavailable => !IsAvailable;

    /// <summary>What the collapsed row says on its right-hand side, so the choice is readable unopened.</summary>
    public string ChosenDescription => IsAvailable ? "Available" : "Unavailable";

    [RelayCommand]
    private void ChooseAvailable() => Choose(ChosenAvailability.Available);

    [RelayCommand]
    private void ChooseUnavailable() => Choose(ChosenAvailability.Unavailable);

    /// <summary>
    /// Deliberately leaves the *menu* open, though it folds the choices away. Setting a status is not
    /// leaving the menu, and closing it would hide the dot the reader just changed before they could see
    /// it change.
    /// </summary>
    private void Choose(ChosenAvailability availability)
    {
        _presence.Choose(availability);
        OnPropertyChanged(nameof(IsAvailable));
        OnPropertyChanged(nameof(IsUnavailable));
        OnPropertyChanged(nameof(ChosenDescription));
        // Folds back up: the choice is made, and the row now shows it.
        IsStatusExpanded = false;
    }

    [RelayCommand]
    private async Task SignOutAsync()
    {
        IsMenuOpen = false;
        await _authenticationClient.SignOutAsync();
        _navigator.ShowSignIn();
    }

    /// <summary>The menu closes on the way out, so coming back does not find it hanging open.</summary>
    private void LeaveMenuFor(Action show)
    {
        IsMenuOpen = false;
        show();
    }

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
