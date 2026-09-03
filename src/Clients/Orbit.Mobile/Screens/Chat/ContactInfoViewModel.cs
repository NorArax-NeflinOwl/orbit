using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Core.Permissions;
using Orbit.Mobile.Api;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Permissions;

namespace Orbit.Mobile.Screens.Chat;

/// <summary>
/// Who somebody is, on one screen: their name, how to reach them, and whether they are here right now.
/// Reached from the contact list and from a conversation - the two places somebody is already looking at
/// a name and wanting to know more about it. The same card Orbit.Web opens at /contacts/{id}.
///
/// Answered from what this phone already holds, and only then asked of the server: a contact card that
/// says nothing without a connection is a card that is blank exactly when somebody is looking up who
/// they are talking to on a train.
/// </summary>
public sealed partial class ContactInfoViewModel : ObservableObject
{
    private readonly ChatRepository _chat;
    private readonly UsersClient _users;
    private readonly UserPermissions _permissions;
    private readonly Translations _translations;
    private readonly IScreenNavigator _navigator;

    private Guid _userId;
    private LocalContact? _contact;

    public ContactInfoViewModel(
        ChatRepository chat, UsersClient users, UserPermissions permissions, Translations translations,
        IScreenNavigator navigator)
    {
        _chat = chat;
        _users = users;
        _permissions = permissions;
        _translations = translations;
        _navigator = navigator;
    }

    [ObservableProperty]
    private string _displayName = string.Empty;

    /// <summary>Their login, which is what somebody is searched for by.</summary>
    [ObservableProperty]
    private string _userName = string.Empty;

    [ObservableProperty]
    private string _email = string.Empty;

    public bool HasEmail => Email.Length > 0;

    /// <summary>Where they are, in the same words the dot beside their name means - see PresenceStatus.</summary>
    [ObservableProperty]
    private string _presence = string.Empty;

    /// <summary>The raw status name, for the dot itself - see PresenceColorConverter.</summary>
    [ObservableProperty]
    private string _presenceStatus = string.Empty;

    [ObservableProperty]
    private string _lastMessage = string.Empty;

    public bool HasLastMessage => LastMessage.Length > 0;

    /// <summary>
    /// What is waiting on somebody, said plainly rather than left to be discovered by a message that
    /// goes nowhere.
    /// </summary>
    [ObservableProperty]
    private string _waitingOn = string.Empty;

    public bool IsWaitingOnSomebody => WaitingOn.Length > 0;

    /// <summary>
    /// Why this screen cannot say more. An account that has not unlocked Contacts is unfindable on
    /// purpose, and Orbit answers a search for it exactly as it answers a search for nobody - so this
    /// cannot tell the two apart, and says so rather than picking one.
    /// </summary>
    [ObservableProperty]
    private string _message = string.Empty;

    public bool HasMessage => Message.Length > 0;

    /// <summary>Whether there is a conversation to open at all - see IsConversationOffered.</summary>
    [ObservableProperty]
    private bool _isConversationOffered;

    /// <summary>Whose card this is, for the avatar that is theirs and stays theirs - see Avatar.</summary>
    [ObservableProperty]
    private Guid _userIdShown;

    public void Open(Guid userId)
    {
        _userId = userId;
        UserIdShown = userId;
        _contact = null;
    }

    [RelayCommand]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        // The stored contact first, and whether or not the account itself resolves: a conversation
        // outlives the profile behind it, and this row is what carries the way into it.
        _contact = (await _chat.GetContactsAsync(cancellationToken))
            .FirstOrDefault(candidate => candidate.UserId == _userId);

        ShowStoredContact();
        await AskTheServerAsync(cancellationToken);
    }

    [RelayCommand]
    private void OpenConversation()
    {
        if (_contact is { } contact)
        {
            _navigator.ShowConversation(contact);
        }
    }

    [RelayCommand]
    private void GoBack() => _navigator.ShowContacts();

    private void ShowStoredContact()
    {
        IsConversationOffered = _contact is not null && _permissions.Has(ApplicationPermission.Chat);

        if (_contact is not { } contact)
        {
            return;
        }

        DisplayName = contact.DisplayName;
        UserName = contact.UserName.Length > 0 ? $"@{contact.UserName}" : string.Empty;
        Email = contact.Email;
        PresenceStatus = contact.PresenceStatus;
        Presence = _translations[contact.PresenceStatus switch
        {
            nameof(Core.Users.PresenceStatus.Available) => "Available",
            nameof(Core.Users.PresenceStatus.Away) => "Away",
            nameof(Core.Users.PresenceStatus.DoNotDisturb) => "Do not disturb",
            _ => "Offline"
        }];

        LastMessage = contact.LastMessageAtUtc == default
            ? string.Empty
            : contact.LastMessageAtUtc.LocalDateTime.ToString("g", _translations.DisplayCulture);

        WaitingOn = contact switch
        {
            { RequiresApprovalFromCurrentUser: true } =>
                _translations["They asked to chat with you. Open the conversation to allow it."],
            { IsPendingApprovalFromOtherParty: true } =>
                _translations["Waiting for them to allow this conversation."],
            _ => string.Empty
        };

        // Nobody can be written to until they have logged in somewhere and made themselves a key.
        Message = contact.PublicKeyBase64 is null
            ? _translations.Format(
                "{0} hasn't set up encryption yet - they need to log in in their own browser first before you can message them.",
                contact.DisplayName)
            : string.Empty;
    }

    /// <summary>
    /// Fills in what only the account itself can say, and corrects a stored name that has since changed.
    /// Anything unreachable leaves what this phone holds standing - it is not less true for being older.
    /// </summary>
    private async Task AskTheServerAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (await _users.FindAsync(_userId, cancellationToken) is { } user)
            {
                DisplayName = user.DisplayName;
                UserName = $"@{user.UserName}";
                return;
            }

            // No such account to this reader: either it never existed, or it belongs to somebody who
            // cannot be looked up. What is already on screen is what there is to say.
            Message = _contact is null
                ? _translations["There is nothing to show for this account. Either it does not exist, or the person has made themselves unfindable - Orbit answers both the same way, on purpose."]
                : _translations["Your conversation with them is not affected - everything in it is still there, and still readable."];
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            // Offline is not an answer about somebody, so the stored row stands and says nothing new.
        }
    }

    partial void OnEmailChanged(string value) => OnPropertyChanged(nameof(HasEmail));

    partial void OnLastMessageChanged(string value) => OnPropertyChanged(nameof(HasLastMessage));

    partial void OnWaitingOnChanged(string value) => OnPropertyChanged(nameof(IsWaitingOnSomebody));

    partial void OnMessageChanged(string value) => OnPropertyChanged(nameof(HasMessage));
}
