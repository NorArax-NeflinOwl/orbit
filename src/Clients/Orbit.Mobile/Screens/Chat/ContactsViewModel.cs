using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Mobile.Api;
using Orbit.Mobile.Crypto;
using Orbit.Mobile.Data;
using Orbit.Core.Permissions;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Permissions;
using Orbit.Mobile.Sync;

namespace Orbit.Mobile.Screens.Chat;

/// <summary>
/// Who the user can talk to. Read from the local cache and then refreshed, so the list opens with no
/// connection - without that, a conversation whose history is cached still could not be reached, which
/// made offline chat readable in principle and not in practice.
/// </summary>
public sealed partial class ContactsViewModel : ObservableObject
{
    private readonly ChatRepository _chatRepository;
    private readonly ChatClient _chatClient;
    private readonly UsersClient _usersClient;
    private readonly ChatSynchronizer _synchronizer;
    private readonly OwnEncryptionKeyProvider _encryptionKeyProvider;
    private readonly Translations _translations;
    private readonly UserPermissions _permissions;
    private readonly IScreenNavigator _navigator;

    [ObservableProperty]
    private string _message = string.Empty;

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    /// <summary>Whoever the last search turned up, or null when it found nobody or none has run.</summary>
    [ObservableProperty]
    private LocalContact? _foundPerson;

    public ContactsViewModel(
        ChatRepository chatRepository, ChatClient chatClient, UsersClient usersClient,
        ChatSynchronizer synchronizer, OwnEncryptionKeyProvider encryptionKeyProvider,
        Translations translations, UserPermissions permissions, IScreenNavigator navigator,
        ConnectionRequirement connection)
    {
        _chatRepository = chatRepository;
        _chatClient = chatClient;
        _usersClient = usersClient;
        _synchronizer = synchronizer;
        _encryptionKeyProvider = encryptionKeyProvider;
        _translations = translations;
        _permissions = permissions;
        _navigator = navigator;
        Connection = connection;
    }

    /// <summary>
    /// True while this account cannot hold a one-to-one conversation. The screen shows why instead of
    /// an empty list, which would claim there is nothing to show - see LockedFeatureMessage.
    /// </summary>
    public bool IsLocked => !_permissions.Has(ApplicationPermission.Contacts);

    public bool IsUnlocked => !IsLocked;

    public string LockedExplanation => LockedFeatureMessage.For(ApplicationPermission.Contacts, _translations);

    public ObservableCollection<LocalContact> Contacts { get; } = [];

    public bool HasMessage => Message.Length > 0;

    /// <summary>
    /// Finding somebody new is the one thing here that cannot be answered from this phone: there
    /// is no local copy of everybody who has an Orbit account, and there should not be. The rest of
    /// the screen - the contacts already known - reads offline like everything else.
    /// </summary>
    public ConnectionRequirement Connection { get; }

    public bool HasFoundSomebody => FoundPerson is not null;

    /// <summary>
    /// Looks somebody up so a conversation can be started with them, which is otherwise impossible from
    /// this device: the list below only holds people already spoken to.
    ///
    /// The whole address has to be typed - the server matches an email address or a username exactly, so
    /// that the search cannot be used to enumerate accounts. The message below says so, because a
    /// partial name silently finding nobody would otherwise read as "they are not on Orbit".
    /// </summary>
    [RelayCommand]
    private async Task SearchAsync(CancellationToken cancellationToken)
    {
        FoundPerson = null;
        Message = string.Empty;

        var identifier = SearchQuery.Trim();
        if (identifier.Length == 0)
        {
            return;
        }

        try
        {
            if (await _usersClient.SearchAsync(identifier, cancellationToken) is not { } found)
            {
                Message = _translations["Nobody has that email address or username. It has to match exactly."];
                return;
            }

            FoundPerson = LocalContact.ForSomebodyNotYetSpokenTo(
                found.Id, found.UserName, found.DisplayName, found.PublicKeyBase64);
        }
        catch (HttpRequestException)
        {
            Message = _translations["Finding somebody new needs a connection."];
        }
        catch (OperationCanceledException)
        {
            // The screen went away mid-search.
        }
    }

    [RelayCommand]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        Message = string.Empty;

        // Nothing behind this screen exists for an account that cannot hold a conversation, and the
        // chat-key gate below would be a strange thing to demand of somebody who cannot chat at all.
        if (IsLocked)
        {
            return;
        }

        // Chat is unreadable without the key, so ask for it before showing a list that goes nowhere.
        if (!await _encryptionKeyProvider.HasKeyAsync(cancellationToken))
        {
            _navigator.ShowChatKeyGate();
            return;
        }

        IsRefreshing = true;
        try
        {
            // What is already known first, so the list is never blank while a request is in flight.
            await ShowCachedContactsAsync(cancellationToken);

            if (await _synchronizer.SynchroniseContactsAsync(cancellationToken))
            {
                await ShowCachedContactsAsync(cancellationToken);
            }
            else
            {
                Message = Contacts.Count == 0
                    ? _translations["Offline, and this device hasn't seen your conversations yet."]
                    : _translations["Offline - showing what's on this phone"];
            }
        }
        catch (HttpRequestException)
        {
            // The server was reached and refused - an expired session, most often. TokenRefreshService
            // has already cleared it and AppNavigator is watching, so the app is on its way to sign-in;
            // what matters here is that this does not escape. These commands are started from
            // OnAppearing without being awaited, and an unobserved failure kills the process.
            Message = _translations["Couldn't refresh just now"];
        }
        catch (OperationCanceledException)
        {
            // The screen went away mid-load; the command is started without being awaited.
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private async Task ShowCachedContactsAsync(CancellationToken cancellationToken)
    {
        var contacts = await _chatRepository.GetContactsAsync(cancellationToken);
        Contacts.Clear();
        foreach (var contact in contacts)
        {
            Contacts.Add(contact);
        }

        Message = Contacts.Count == 0 ? _translations["No conversations yet."] : string.Empty;
    }

    [RelayCommand]
    private void OpenConversation(LocalContact? contact)
    {
        if (contact is null)
        {
            return;
        }

        SearchQuery = string.Empty;
        FoundPerson = null;
        _navigator.ShowConversation(contact);
    }

    /// <summary>
    /// Allows a conversation somebody else started. Until this happens the server refuses everything sent
    /// back, so without it a chat request that arrived from another device could be read and never
    /// answered. Needs a connection: it is the server's record of consent, not the phone's.
    /// </summary>
    [RelayCommand]
    private async Task AcceptAsync(LocalContact? contact, CancellationToken cancellationToken)
    {
        if (contact is null)
        {
            return;
        }

        try
        {
            await _chatClient.ApproveConversationAsync(contact.UserId, cancellationToken);
        }
        catch (HttpRequestException)
        {
            Message = _translations["Accepting a chat request needs a connection."];
            return;
        }
        catch (OperationCanceledException)
        {
            return;
        }

        await LoadAsync(cancellationToken);
    }

    [RelayCommand]
    private void OpenGroups() => _navigator.ShowGroups();

    [RelayCommand]
    private void OpenAccount() => _navigator.ShowAccount();

    partial void OnMessageChanged(string value) => OnPropertyChanged(nameof(HasMessage));

    partial void OnFoundPersonChanged(LocalContact? value) => OnPropertyChanged(nameof(HasFoundSomebody));
}
