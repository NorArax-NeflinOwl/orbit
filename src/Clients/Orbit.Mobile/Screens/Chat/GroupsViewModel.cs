using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Contracts.Chat;
using Orbit.Mobile.Api;
using Orbit.Mobile.Crypto;
using Orbit.Mobile.Data;
using Orbit.Core.Permissions;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Permissions;
using Orbit.Mobile.Sync;

namespace Orbit.Mobile.Screens.Chat;

/// <summary>
/// The user's group conversations. Read from the local cache and then refreshed, so the list opens with
/// no connection - the same reason <see cref="ContactsViewModel"/> works that way.
///
/// Creating one is the exception: it needs the server, because a group has no meaning until the server
/// has given it an id and a membership to validate fan-outs against.
/// </summary>
public sealed partial class GroupsViewModel : ObservableObject
{
    private readonly ChatRepository _chatRepository;
    private readonly ChatClient _chatClient;
    private readonly ChatSynchronizer _synchronizer;
    private readonly OwnEncryptionKeyProvider _encryptionKeyProvider;
    private readonly Translations _translations;
    private readonly UserPermissions _permissions;
    private readonly ConversationPins _pins;
    private readonly IScreenNavigator _navigator;

    [ObservableProperty]
    private string _message = string.Empty;

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    private bool _isCreating;

    [ObservableProperty]
    private string _newGroupName = string.Empty;

    public GroupsViewModel(
        ChatRepository chatRepository, ChatClient chatClient, ChatSynchronizer synchronizer,
        OwnEncryptionKeyProvider encryptionKeyProvider, Translations translations, UserPermissions permissions,
        IScreenNavigator navigator, ConversationPins pins)
    {
        _pins = pins;
        _chatRepository = chatRepository;
        _chatClient = chatClient;
        _synchronizer = synchronizer;
        _encryptionKeyProvider = encryptionKeyProvider;
        _translations = translations;
        _permissions = permissions;
        _navigator = navigator;
    }

    /// <summary>True while this account cannot hold a group conversation - see LockedFeatureMessage.</summary>
    public bool IsLocked => !_permissions.Has(ApplicationPermission.Chat);

    public bool IsUnlocked => !IsLocked;

    public string LockedExplanation => LockedFeatureMessage.For(ApplicationPermission.Chat, _translations);

    public ObservableCollection<LocalChatGroup> Groups { get; } = [];

    /// <summary>Who can be put in a new group: the people this phone knows about.</summary>
    public ObservableCollection<SelectableContact> Candidates { get; } = [];

    public bool HasMessage => Message.Length > 0;

    /// <summary>The list and the create panel replace each other, and XAML has no "not".</summary>
    public bool IsNotCreating => !IsCreating;

    [RelayCommand]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        Message = string.Empty;

        // Nothing behind this screen exists for an account that cannot hold a group conversation, and
        // demanding a chat key of somebody who cannot use one would be a strange way to say so.
        if (IsLocked)
        {
            return;
        }

        // A group conversation is unreadable without the key, so ask for it before showing a list that
        // goes nowhere - exactly as the contact list does.
        if (!await _encryptionKeyProvider.HasKeyAsync(cancellationToken))
        {
            _navigator.ShowChatKeyGate();
            return;
        }

        IsRefreshing = true;
        try
        {
            await ShowCachedGroupsAsync(cancellationToken);

            if (await _synchronizer.SynchroniseGroupsAsync(cancellationToken))
            {
                await ShowCachedGroupsAsync(cancellationToken);
            }
            else
            {
                Message = Groups.Count == 0
                    ? _translations["Offline, and this device hasn't seen your groups yet."]
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

    [RelayCommand]
    private async Task StartCreatingAsync(CancellationToken cancellationToken)
    {
        Message = string.Empty;
        NewGroupName = string.Empty;

        Candidates.Clear();
        foreach (var contact in await _chatRepository.GetContactsAsync(cancellationToken))
        {
            Candidates.Add(new SelectableContact(contact));
        }

        if (Candidates.Count == 0)
        {
            Message = _translations["You have nobody to add yet - start a conversation first."];
            return;
        }

        IsCreating = true;
    }

    [RelayCommand]
    private void CancelCreating()
    {
        IsCreating = false;
        Message = string.Empty;
    }

    [RelayCommand]
    private async Task CreateAsync(CancellationToken cancellationToken)
    {
        var name = NewGroupName.Trim();
        if (name.Length == 0)
        {
            Message = _translations["Give the group a name."];
            return;
        }

        var members = Candidates.Where(candidate => candidate.IsSelected).Select(candidate => candidate.Contact.UserId).ToList();
        if (members.Count == 0)
        {
            Message = _translations["Pick at least one person."];
            return;
        }

        try
        {
            await _chatClient.CreateGroupAsync(new CreateChatGroupRequest(name, members), cancellationToken);
        }
        catch (HttpRequestException)
        {
            Message = _translations["Creating a group needs a connection."];
            return;
        }
        catch (OperationCanceledException)
        {
            return;
        }

        IsCreating = false;
        await LoadAsync(cancellationToken);
    }

    [RelayCommand]
    private void OpenGroup(LocalChatGroup? group)
    {
        if (group is not null)
        {
            _navigator.ShowGroupConversation(group);
        }
    }

    [RelayCommand]
    private void OpenAccount() => _navigator.ShowAccount();

    [RelayCommand]
    private void GoBack() => _navigator.ShowContacts();

    private async Task ShowCachedGroupsAsync(CancellationToken cancellationToken)
    {
        var groups = await _chatRepository.GetGroupsAsync(cancellationToken);
        HasArchive = groups.Any(group => group.IsArchived);

        // <inheritdoc cref="ContactsViewModel.ShowCachedContactsAsync" />
        if (!HasArchive)
        {
            IsShowingArchive = false;
        }

        Groups.Clear();
        foreach (var group in InReadingOrder(groups.Where(group => group.IsArchived == IsShowingArchive)))
        {
            group.IsPinned = _pins.IsPinned(group.Id);
            Groups.Add(group);
        }

        Message = Groups.Count == 0
            ? IsShowingArchive ? _translations["Nothing put away."] : _translations["No groups yet."]
            : string.Empty;
    }

    /// <inheritdoc cref="ContactsViewModel.InReadingOrder"/>
    private IEnumerable<LocalChatGroup> InReadingOrder(IEnumerable<LocalChatGroup> groups)
        => IsShowingArchive ? groups : _pins.PinnedFirst(groups, group => group.Id);

    /// <inheritdoc cref="ContactsViewModel.TogglePinAsync"/>
    [RelayCommand]
    private Task TogglePinAsync(LocalChatGroup? group, CancellationToken cancellationToken)
    {
        if (group is null)
        {
            return Task.CompletedTask;
        }

        _pins.Toggle(group.Id);
        return ShowCachedGroupsAsync(cancellationToken);
    }

    /// <summary>
    /// Puts a group away on this reader's list, or brings it back. The flag lives on their own
    /// membership, so this takes it off nobody else's list and needs no rank at all - see
    /// ChatClient.SetGroupArchivedAsync. A member who puts a group away is still in it and still
    /// receives what is posted; leaving is the other thing.
    /// </summary>
    [RelayCommand]
    private async Task SetArchivedAsync(LocalChatGroup? group, CancellationToken cancellationToken)
    {
        if (group is null)
        {
            return;
        }

        try
        {
            if (!await _chatClient.SetGroupArchivedAsync(group.Id, !group.IsArchived, cancellationToken))
            {
                Message = _translations["Orbit has no such group any more."];
                return;
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            Message = group.IsArchived
                ? _translations["Could not put that back. Check your connection and try again."]
                : _translations["Could not put that away. Check your connection and try again."];
            return;
        }

        await RefreshFromTheServerAsync(cancellationToken);
    }

    /// <summary>
    /// Leaves the group for good. Unlike putting it away, the rest of the group sees somebody go and
    /// nothing posted afterwards arrives - which is why the screen asks first.
    /// </summary>
    [RelayCommand]
    private async Task LeaveAsync(LocalChatGroup? group, CancellationToken cancellationToken)
    {
        if (group is null)
        {
            return;
        }

        try
        {
            if (!await _chatClient.LeaveGroupAsync(group.Id, cancellationToken))
            {
                Message = _translations["Orbit has no such group any more."];
                return;
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            Message = _translations["Could not leave that group. Check your connection and try again."];
            return;
        }

        await RefreshFromTheServerAsync(cancellationToken);
    }

    /// <inheritdoc cref="ContactsViewModel.RefreshFromTheServerAsync"/>
    private async Task RefreshFromTheServerAsync(CancellationToken cancellationToken)
    {
        await _synchronizer.SynchroniseGroupsAsync(cancellationToken);
        await ShowCachedGroupsAsync(cancellationToken);
    }

    partial void OnIsShowingArchiveChanged(bool value) => LoadCommand.Execute(null);

    /// <inheritdoc cref="ContactsViewModel.IsShowingArchive"/>
    [ObservableProperty]
    private bool _isShowingArchive;

    /// <inheritdoc cref="ContactsViewModel.HasArchive"/>
    [ObservableProperty]
    private bool _hasArchive;

    partial void OnMessageChanged(string value) => OnPropertyChanged(nameof(HasMessage));

    partial void OnIsCreatingChanged(bool value) => OnPropertyChanged(nameof(IsNotCreating));
}
