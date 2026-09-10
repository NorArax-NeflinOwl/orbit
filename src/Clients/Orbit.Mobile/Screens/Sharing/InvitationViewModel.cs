using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Mobile.Api;
using Orbit.Mobile.Chat;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Notifications;
using Orbit.Mobile.Sync;

namespace Orbit.Mobile.Screens.Sharing;

/// <summary>
/// Something offered to this reader, on a screen of its own - the phone's answer to Orbit.Web's
/// /invitation/{kind}/{shareId}/{sharerUserId} page, reached the same way: by pressing the notification
/// that says somebody shared something.
///
/// The phone had no such screen until 2026-09-10. The notification took the last segment of that path -
/// who offered it - and opened the conversation, where an offer arrives as a chat message with its own
/// Accept. That is fine as long as the message can be read, and the case it cannot is exactly the one
/// this covers: a message sealed to a key this device does not hold, a conversation whose history was
/// never pulled down, an offer made from a screen whose chat half failed. The share row is on the
/// server either way, so the offer can be read and taken up without the message at all.
///
/// Accepting does not fetch the thing. It creates the copy on the server, and the feature's own screen
/// pulls it down when it is opened - which is why "open it" here is the section rather than the thing:
/// the path names the item by the server's id and every screen on this phone is opened by the local one.
/// </summary>
public sealed partial class InvitationViewModel : ObservableObject
{
    private readonly ShareOfferClient _offers;
    private readonly SharedItemAcceptance _acceptance;
    private readonly UsersClient _users;
    private readonly INetworkStatus _networkStatus;
    private readonly Translations _translations;
    private readonly IScreenNavigator _navigator;

    private InvitationOffer? _offer;

    /// <summary>What was offered, by name. Empty for something deleted since - the offer still stands.</summary>
    [ObservableProperty]
    private string _title = string.Empty;

    /// <summary>Which kind of thing it is, in the reader's language.</summary>
    [ObservableProperty]
    private string _kind = string.Empty;

    [ObservableProperty]
    private string _sharedBy = string.Empty;

    [ObservableProperty]
    private string _message = string.Empty;

    [ObservableProperty]
    private bool _isLoading = true;

    /// <summary>
    /// Whether there is an offer to show at all. False covers every way reading one fails, and the
    /// screen says one sentence for all of them - see ShareOfferClient.ReadAsync.
    /// </summary>
    [ObservableProperty]
    private bool _wasFound;

    /// <summary>False once it has been taken up, and for an offer that was already taken up elsewhere.</summary>
    [ObservableProperty]
    private bool _canBeAccepted;

    /// <summary>True once this account has it - taken up here, or taken up before this screen opened.</summary>
    [ObservableProperty]
    private bool _isHeld;

    [ObservableProperty]
    private bool _isAccepting;

    public InvitationViewModel(
        ShareOfferClient offers, SharedItemAcceptance acceptance, UsersClient users,
        INetworkStatus networkStatus, Translations translations, IScreenNavigator navigator)
    {
        _offers = offers;
        _acceptance = acceptance;
        _users = users;
        _networkStatus = networkStatus;
        _translations = translations;
        _navigator = navigator;
    }

    public bool HasMessage => Message.Length > 0;

    /// <summary>What the Accept button is enabled by, since the markup has no way to say "not".</summary>
    public bool IsNotAccepting => !IsAccepting;

    public bool HasTitle => Title.Length > 0;

    /// <summary>Whether there is somebody to answer in a conversation - see <see cref="MessageThemCommand"/>.</summary>
    public bool HasSharer => _offer is not null;

    partial void OnMessageChanged(string value) => OnPropertyChanged(nameof(HasMessage));

    partial void OnTitleChanged(string value) => OnPropertyChanged(nameof(HasTitle));

    partial void OnIsAcceptingChanged(bool value) => OnPropertyChanged(nameof(IsNotAccepting));

    /// <summary>The offer this screen was opened for - see NotificationDestination.</summary>
    public void Open(InvitationOffer offer)
    {
        _offer = offer;
        OnPropertyChanged(nameof(HasSharer));
    }

    [RelayCommand]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (_offer is not { } offer)
        {
            return;
        }

        IsLoading = true;
        try
        {
            var read = await _offers.ReadAsync(offer.KindPath, offer.ShareId, cancellationToken);

            WasFound = read is not null;
            Kind = NameOf(offer.Kind);
            SharedBy = await SharerAsync(offer.SharerUserId, cancellationToken);

            if (read is null)
            {
                Title = string.Empty;
                IsHeld = false;
                CanBeAccepted = false;
                Message = _translations["This offer is no longer there. Whoever made it may have taken it back."];
                return;
            }

            // Empty for something deleted since it was offered. The offer stands, so the screen names
            // the kind and leaves the rest to the section it lands in.
            Title = read.ItemTitle;
            IsHeld = read.IsAccepted;
            CanBeAccepted = !read.IsAccepted;
            Message = read.IsAccepted ? _translations["You already have this."] : string.Empty;
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            WasFound = false;
            CanBeAccepted = false;
            Message = _networkStatus.IsOnline
                ? _translations["This invitation couldn't be opened. Try again."]
                : _translations["An invitation can only be opened online. Try again when you are back."];
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Takes it up. The same call the conversation's own Accept makes, on the same one place that knows
    /// which endpoint each kind is accepted at - see SharedItemAcceptance.
    /// </summary>
    [RelayCommand]
    private async Task AcceptAsync(CancellationToken cancellationToken)
    {
        if (_offer is not { } offer)
        {
            return;
        }

        IsAccepting = true;
        try
        {
            var accepted = await _acceptance.AcceptAsync(
                new SharedItemInvitation(offer.Kind, offer.ShareId, Title), cancellationToken);

            if (!accepted)
            {
                Message = _translations["This couldn't be added to your account. Try again."];
                return;
            }

            CanBeAccepted = false;
            IsHeld = true;
            Message = _translations["Saved. It will appear once Orbit next syncs."];
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            Message = _networkStatus.IsOnline
                ? _translations["This couldn't be added to your account. Try again."]
                : _translations["An invitation can only be accepted online. Try again when you are back."];
        }
        finally
        {
            IsAccepting = false;
        }
    }

    /// <summary>
    /// Where it will appear. The section rather than the thing, because the offer names it by the
    /// server's id and every screen here is opened by the phone's own - and the section pulls the new
    /// copy down as it loads, which is what makes it there to find.
    /// </summary>
    [RelayCommand]
    private void OpenWhereItLanded()
    {
        switch (_offer?.Kind)
        {
            case SharedItemKind.Note:
                _navigator.ShowNotes();
                break;
            case SharedItemKind.TaskList:
                _navigator.ShowTasks();
                break;
            case SharedItemKind.CalendarEvent:
                _navigator.ShowCalendar();
                break;
            case SharedItemKind.Place:
                _navigator.ShowPlaces();
                break;
            default:
                _navigator.ShowInventory();
                break;
        }
    }

    /// <summary>
    /// Who offered it, in case the reader would rather answer them than answer the offer - and where
    /// this notification led before this screen existed.
    /// </summary>
    [RelayCommand]
    private void MessageThem()
    {
        if (_offer is { } offer)
        {
            _navigator.ShowContactInfo(offer.SharerUserId);
        }
    }

    [RelayCommand]
    private void GoBack() => _navigator.ShowDashboard();

    /// <summary>
    /// Their name, or nothing. Nothing rather than an id or a placeholder: the sentence reads
    /// "Shared with you" without a name, which is honest, and an account this phone cannot look up is
    /// not a reason to leave the offer unreadable.
    /// </summary>
    private async Task<string> SharerAsync(Guid sharerUserId, CancellationToken cancellationToken)
    {
        try
        {
            return await _users.FindAsync(sharerUserId, cancellationToken) is { } person
                ? _translations.Format("Shared by {0}", person.DisplayName)
                : _translations["Shared with you"];
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            return _translations["Shared with you"];
        }
    }

    private string NameOf(SharedItemKind kind) => kind switch
    {
        SharedItemKind.Note => _translations["Note"],
        SharedItemKind.TaskList => _translations["Task list"],
        SharedItemKind.CalendarEvent => _translations["Event"],
        SharedItemKind.Place => _translations["Place"],
        _ => _translations["Inventory"]
    };
}
