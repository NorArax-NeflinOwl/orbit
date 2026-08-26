using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Contracts.Chat;
using Orbit.Mobile.Api;
using Orbit.Mobile.Crypto;
using Orbit.Mobile.Data;
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
        OwnEncryptionKeyProvider encryptionKeyProvider, IScreenNavigator navigator)
    {
        _chatRepository = chatRepository;
        _chatClient = chatClient;
        _synchronizer = synchronizer;
        _encryptionKeyProvider = encryptionKeyProvider;
        _navigator = navigator;
    }

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
                    ? "Offline, and this device hasn't seen your groups yet."
                    : "Offline - showing what's on this phone";
            }
        }
        catch (HttpRequestException)
        {
            // The server was reached and refused - an expired session, most often. TokenRefreshService
            // has already cleared it and AppNavigator is watching, so the app is on its way to sign-in;
            // what matters here is that this does not escape. These commands are started from
            // OnAppearing without being awaited, and an unobserved failure kills the process.
            Message = "Couldn't refresh just now";
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
            Message = "You have nobody to add yet - start a conversation first.";
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
            Message = "Give the group a name.";
            return;
        }

        var members = Candidates.Where(candidate => candidate.IsSelected).Select(candidate => candidate.Contact.UserId).ToList();
        if (members.Count == 0)
        {
            Message = "Pick at least one person.";
            return;
        }

        try
        {
            await _chatClient.CreateGroupAsync(new CreateChatGroupRequest(name, members), cancellationToken);
        }
        catch (HttpRequestException)
        {
            Message = "Creating a group needs a connection.";
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
    private void GoBack() => _navigator.ShowContacts();

    private async Task ShowCachedGroupsAsync(CancellationToken cancellationToken)
    {
        var groups = await _chatRepository.GetGroupsAsync(cancellationToken);
        Groups.Clear();
        foreach (var group in groups)
        {
            Groups.Add(group);
        }

        Message = Groups.Count == 0 ? "No groups yet." : string.Empty;
    }

    partial void OnMessageChanged(string value) => OnPropertyChanged(nameof(HasMessage));

    partial void OnIsCreatingChanged(bool value) => OnPropertyChanged(nameof(IsNotCreating));
}
