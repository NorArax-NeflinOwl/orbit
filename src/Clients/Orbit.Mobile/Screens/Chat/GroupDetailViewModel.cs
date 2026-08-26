using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Mobile.Api;
using Orbit.Mobile.Authentication;
using Orbit.Mobile.Data;
using Orbit.Mobile.Sync;

namespace Orbit.Mobile.Screens.Chat;

/// <summary>
/// Who is in a group, and - for an admin - changing it: adding somebody, taking somebody out, handing
/// out or taking back the admin role.
///
/// Every rule about what is allowed lives on the server and is left there. The screen offers only what
/// an admin could plausibly do, and when the server refuses anyway it repeats the server's own wording:
/// "A group needs at least one admin - promote someone else first" is better than anything this could
/// reconstruct, and it cannot drift out of step with the rule it describes.
/// </summary>
public sealed partial class GroupDetailViewModel : ObservableObject
{
    private readonly ChatRepository _chatRepository;
    private readonly ChatClient _chatClient;
    private readonly ChatSynchronizer _synchronizer;
    private readonly SessionStore _sessionStore;
    private readonly IScreenNavigator _navigator;

    private LocalChatGroup? _group;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _message = string.Empty;

    [ObservableProperty]
    private bool _isAdmin;

    [ObservableProperty]
    private bool _isAdding;

    public GroupDetailViewModel(
        ChatRepository chatRepository, ChatClient chatClient, ChatSynchronizer synchronizer,
        SessionStore sessionStore, IScreenNavigator navigator)
    {
        _chatRepository = chatRepository;
        _chatClient = chatClient;
        _synchronizer = synchronizer;
        _sessionStore = sessionStore;
        _navigator = navigator;
    }

    public ObservableCollection<GroupMemberRow> Members { get; } = [];

    /// <summary>People this account has a conversation with who are not in the group yet.</summary>
    public ObservableCollection<SelectableContact> Candidates { get; } = [];

    public bool HasMessage => Message.Length > 0;

    public bool IsNotAdding => !IsAdding;

    /// <summary>
    /// Whether to offer starting to add somebody. Both this and the Add/Cancel pair sit in the same row -
    /// they replace each other - so this has to go while that is showing, or they overlap.
    /// </summary>
    public bool CanOfferAdding => IsAdmin && IsNotAdding;

    public void Open(LocalChatGroup group)
    {
        _group = group;
        Title = group.Name;
    }

    [RelayCommand]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (_group is null)
        {
            return;
        }

        // Both refreshed before anything is offered. The membership, because acting on one this phone
        // last saw an hour ago is how an admin ends up demoting somebody who already left. The contacts,
        // because they are who may be added - and they are cached by a different screen, so a group
        // opened without visiting that one first would offer nobody at all.
        try
        {
            await _synchronizer.SynchroniseGroupsAsync(cancellationToken);
            await _synchronizer.SynchroniseContactsAsync(cancellationToken);
        }
        catch (HttpRequestException)
        {
            // See ContactsViewModel: refused rather than unreachable, and it must not escape a command
            // nobody is awaiting. What is already stored is still worth showing.
            Message = "Couldn't refresh just now";
        }
        catch (OperationCanceledException)
        {
            return;
        }

        await ShowStoredGroupAsync(cancellationToken);
    }

    [RelayCommand]
    private async Task StartAddingAsync(CancellationToken cancellationToken)
    {
        Message = string.Empty;
        Candidates.Clear();

        var alreadyIn = Members.Select(member => member.UserId).ToHashSet();
        foreach (var contact in await _chatRepository.GetContactsAsync(cancellationToken))
        {
            if (!alreadyIn.Contains(contact.UserId))
            {
                Candidates.Add(new SelectableContact(contact));
            }
        }

        if (Candidates.Count == 0)
        {
            // The server's rule, said before it has to refuse: a group cannot be used to reach somebody
            // who never agreed to hear from you.
            Message = "Everybody you have a conversation with is already in this group.";
            return;
        }

        IsAdding = true;
    }

    [RelayCommand]
    private void CancelAdding()
    {
        IsAdding = false;
        Message = string.Empty;
    }

    [RelayCommand]
    private async Task AddSelectedAsync(CancellationToken cancellationToken)
    {
        var chosen = Candidates.Where(candidate => candidate.IsSelected).Select(candidate => candidate.Contact.UserId).ToList();
        if (chosen.Count == 0)
        {
            Message = "Pick at least one person.";
            return;
        }

        foreach (var userId in chosen)
        {
            if (!await ApplyAsync(group => _chatClient.AddGroupMemberAsync(group, userId, cancellationToken), cancellationToken))
            {
                return;
            }
        }

        IsAdding = false;
        await LoadAsync(cancellationToken);
    }

    [RelayCommand]
    private async Task RemoveAsync(GroupMemberRow? member, CancellationToken cancellationToken)
    {
        if (member is not { CanBeRemoved: true })
        {
            return;
        }

        if (!await ApplyAsync(group => _chatClient.RemoveGroupMemberAsync(group, member.UserId, cancellationToken), cancellationToken))
        {
            return;
        }

        // Removing yourself is how leaving works, and there is nothing left to look at afterwards.
        if (member.IsSelf)
        {
            await _synchronizer.SynchroniseGroupsAsync(cancellationToken);
            _navigator.ShowGroups();
            return;
        }

        await LoadAsync(cancellationToken);
    }

    [RelayCommand]
    private Task PromoteAsync(GroupMemberRow? member, CancellationToken cancellationToken)
        => member is { CanBePromoted: true } ? ChangeRoleAsync(member, "Admin", cancellationToken) : Task.CompletedTask;

    [RelayCommand]
    private Task DemoteAsync(GroupMemberRow? member, CancellationToken cancellationToken)
        => member is { CanBeDemoted: true } ? ChangeRoleAsync(member, "Member", cancellationToken) : Task.CompletedTask;

    [RelayCommand]
    private void GoBack() => _navigator.ShowGroups();

    private async Task ChangeRoleAsync(GroupMemberRow member, string role, CancellationToken cancellationToken)
    {
        if (await ApplyAsync(group => _chatClient.ChangeGroupMemberRoleAsync(group, member.UserId, role, cancellationToken), cancellationToken))
        {
            await LoadAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Runs one membership change and turns whatever came back into something to read. False means the
    /// caller should stop rather than carry on to the next change.
    /// </summary>
    private async Task<bool> ApplyAsync(
        Func<Guid, Task<GroupMemberChangeResult>> change, CancellationToken cancellationToken)
    {
        if (_group is null)
        {
            return false;
        }

        Message = string.Empty;
        try
        {
            var result = await change(_group.Id);
            if (result.Done)
            {
                return true;
            }

            Message = result.Refusal ?? "This group is no longer available.";
            return false;
        }
        catch (HttpRequestException)
        {
            Message = "Changing who is in a group needs a connection.";
            return false;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private async Task ShowStoredGroupAsync(CancellationToken cancellationToken)
    {
        if (_group is null || await _chatRepository.FindGroupAsync(_group.Id, cancellationToken) is not { } group)
        {
            _navigator.ShowGroups();
            return;
        }

        _group = group;
        Title = group.Name;
        IsAdmin = group.OwnRole == "Admin";

        var ownUserId = await _sessionStore.GetAsync() is { } session ? session.UserId : Guid.Empty;
        Members.Clear();
        foreach (var member in group.Members.OrderByDescending(member => member.Role == "Admin").ThenBy(member => member.DisplayName))
        {
            Members.Add(GroupMemberRow.From(member, ownUserId, IsAdmin));
        }
    }

    partial void OnMessageChanged(string value) => OnPropertyChanged(nameof(HasMessage));

    partial void OnIsAddingChanged(bool value)
    {
        OnPropertyChanged(nameof(IsNotAdding));
        OnPropertyChanged(nameof(CanOfferAdding));
    }

    partial void OnIsAdminChanged(bool value) => OnPropertyChanged(nameof(CanOfferAdding));
}
