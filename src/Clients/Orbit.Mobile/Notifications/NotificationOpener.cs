using Orbit.Mobile.Api;
using Orbit.Mobile.Data;
using Orbit.Mobile.Screens;
using Orbit.Mobile.Sync;

namespace Orbit.Mobile.Notifications;

/// <summary>
/// Why a notification could not be opened. The caller shows this rather than failing silently: a tap
/// that does nothing at all is the worst outcome, because it reads as the app being broken.
/// </summary>
public enum NotificationOpenOutcome
{
    Opened,

    /// <summary>The path is not one this build knows - an older app against a newer server.</summary>
    NowhereToGo,

    /// <summary>The conversation or group exists, but this phone has never heard of it and could not ask.</summary>
    NotOnThisPhoneYet
}

/// <summary>
/// Takes the reader where a notification points.
///
/// The awkward part is that the server identifies a conversation by the other person's user id, and
/// the screen needs the contact row itself. Two different things can go wrong there, and only one of
/// them is a staleness problem:
///
/// - The phone has simply not caught up. A first message arrives as a notification *before* any sync
///   has pulled the sender into the contact list, so a miss refreshes contacts and looks again.
/// - The person is genuinely not a contact at all. The server counts somebody as a contact once there
///   has been a conversation, but a shared note notifies with the same /chat/{userId} path - so
///   refreshing forever would never find them. They are looked up by id instead, which is what
///   LocalContact.ForSomebodyNotYetSpokenTo exists for.
/// </summary>
public sealed class NotificationOpener
{
    private readonly ChatRepository _chatRepository;
    private readonly ChatSynchronizer _synchronizer;
    private readonly UsersClient _usersClient;
    private readonly PendingNotificationTap _pendingTap;
    private readonly IScreenNavigator _navigator;

    public NotificationOpener(
        ChatRepository chatRepository, ChatSynchronizer synchronizer, UsersClient usersClient,
        PendingNotificationTap pendingTap, IScreenNavigator navigator)
    {
        _chatRepository = chatRepository;
        _synchronizer = synchronizer;
        _usersClient = usersClient;
        _pendingTap = pendingTap;
        _navigator = navigator;
    }

    /// <summary>
    /// Follows the notification the reader tapped to get here, if they tapped one. False when there was
    /// nothing waiting or it could not be followed - the caller then sends them wherever they would have
    /// gone anyway, because a cold start must never end on the splash screen.
    /// </summary>
    public async Task<bool> FollowTapThatLaunchedTheAppAsync(CancellationToken cancellationToken = default)
    {
        if (_pendingTap.TakeAtStartup() is not { } url)
        {
            return false;
        }

        return await OpenAsync(url, cancellationToken) == NotificationOpenOutcome.Opened;
    }

    public Task<NotificationOpenOutcome> OpenAsync(string? url, CancellationToken cancellationToken = default)
        => NotificationDestination.Parse(url) is { } destination
            ? OpenAsync(destination, cancellationToken)
            : Task.FromResult(NotificationOpenOutcome.NowhereToGo);

    public async Task<NotificationOpenOutcome> OpenAsync(
        NotificationDestination destination, CancellationToken cancellationToken = default)
    {
        switch (destination.Target)
        {
            case NotificationTarget.Conversation:
                return await OpenConversationAsync(destination.Id, cancellationToken);

            case NotificationTarget.GroupConversation:
                return await OpenGroupAsync(destination.Id, cancellationToken);

            case NotificationTarget.TaskList when destination.Id is { } taskListId:
                _navigator.ShowTaskList(taskListId);
                return NotificationOpenOutcome.Opened;

            case NotificationTarget.Calendar:
                _navigator.ShowCalendar();
                return NotificationOpenOutcome.Opened;

            case NotificationTarget.Inventory:
                _navigator.ShowInventory();
                return NotificationOpenOutcome.Opened;

            case NotificationTarget.Map:
                _navigator.ShowMap();
                return NotificationOpenOutcome.Opened;

            default:
                return NotificationOpenOutcome.NowhereToGo;
        }
    }

    private async Task<NotificationOpenOutcome> OpenConversationAsync(
        Guid? otherUserId, CancellationToken cancellationToken)
    {
        if (otherUserId is not { } userId)
        {
            return NotificationOpenOutcome.NowhereToGo;
        }

        var contact = await FindContactAsync(userId, cancellationToken)
            ?? await RefreshThenFindContactAsync(userId, cancellationToken)
            ?? await LookUpAsync(userId, cancellationToken);
        if (contact is null)
        {
            return NotificationOpenOutcome.NotOnThisPhoneYet;
        }

        _navigator.ShowConversation(contact);
        return NotificationOpenOutcome.Opened;
    }

    private async Task<NotificationOpenOutcome> OpenGroupAsync(Guid? groupId, CancellationToken cancellationToken)
    {
        if (groupId is not { } id)
        {
            return NotificationOpenOutcome.NowhereToGo;
        }

        var group = await _chatRepository.FindGroupAsync(id, cancellationToken)
            ?? await RefreshThenFindGroupAsync(id, cancellationToken);
        if (group is null)
        {
            return NotificationOpenOutcome.NotOnThisPhoneYet;
        }

        _navigator.ShowGroupConversation(group);
        return NotificationOpenOutcome.Opened;
    }

    private async Task<LocalContact?> FindContactAsync(Guid userId, CancellationToken cancellationToken)
        => (await _chatRepository.GetContactsAsync(cancellationToken))
            .FirstOrDefault(contact => contact.UserId == userId);

    /// <summary>
    /// The one retry. Offline it simply fails, which is the honest answer: without a connection there is
    /// no way to learn who this is, and the conversation has nothing local to show anyway.
    /// </summary>
    private async Task<LocalContact?> RefreshThenFindContactAsync(Guid userId, CancellationToken cancellationToken)
        => await _synchronizer.SynchroniseContactsAsync(cancellationToken)
            ? await FindContactAsync(userId, cancellationToken)
            : null;

    /// <summary>
    /// Somebody who is not a contact and never will be until a message passes between them. Deliberately
    /// not stored - the server owns the contact list, and writing this down would put them in it before
    /// any conversation existed. See LocalContact.ForSomebodyNotYetSpokenTo.
    /// </summary>
    private async Task<LocalContact?> LookUpAsync(Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            return await _usersClient.FindAsync(userId, cancellationToken) is { } person
                ? LocalContact.ForSomebodyNotYetSpokenTo(
                    person.Id, person.UserName, person.DisplayName, person.PublicKeyBase64)
                : null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    private async Task<LocalChatGroup?> RefreshThenFindGroupAsync(Guid groupId, CancellationToken cancellationToken)
        => await _synchronizer.SynchroniseGroupsAsync(cancellationToken)
            ? await _chatRepository.FindGroupAsync(groupId, cancellationToken)
            : null;
}
