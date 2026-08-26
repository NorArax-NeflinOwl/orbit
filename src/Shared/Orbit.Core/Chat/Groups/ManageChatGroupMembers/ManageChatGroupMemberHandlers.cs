using Orbit.Core.Abstractions;
using Orbit.Core.Notifications;
using Orbit.Core.Users;

namespace Orbit.Core.Chat.Groups.ManageChatGroupMembers;

/// <summary>
/// All three membership handlers share one shape: load the group, refuse if the caller can't even see
/// it, let ChatGroup decide whether the change itself is allowed, save. False means "no such group, as
/// far as you're concerned"; a refusal the caller is entitled to hear about comes back as an
/// InvalidRequestException from ChatGroup, and so as a 400 naming the rule.
/// </summary>
public sealed class AddChatGroupMemberCommandHandler : IRequestHandler<AddChatGroupMemberCommand, bool>
{
    private readonly IChatGroupRepository _chatGroupRepository;
    private readonly IContactRepository _contactRepository;
    private readonly IUserRepository _userRepository;
    private readonly NotificationRecorder _notificationRecorder;
    private readonly PushNotificationDispatcher _pushNotificationDispatcher;

    public AddChatGroupMemberCommandHandler(
        IChatGroupRepository chatGroupRepository, IContactRepository contactRepository, IUserRepository userRepository,
        NotificationRecorder notificationRecorder, PushNotificationDispatcher pushNotificationDispatcher)
    {
        _chatGroupRepository = chatGroupRepository;
        _contactRepository = contactRepository;
        _userRepository = userRepository;
        _notificationRecorder = notificationRecorder;
        _pushNotificationDispatcher = pushNotificationDispatcher;
    }

    public async Task<bool> HandleAsync(AddChatGroupMemberCommand request, CancellationToken cancellationToken)
    {
        var group = await _chatGroupRepository.GetByIdAsync(request.GroupId, cancellationToken);
        if (group is null || !group.IsMember(request.ActorUserId))
        {
            return false;
        }

        if (group.IsMember(request.UserId))
        {
            // Already in, so this asks for nothing - and asking the adder to have a chat with someone
            // who is right there in the group would be friction for no gain.
            return true;
        }

        // Same rule as at creation time: a group can't be used to reach someone who never agreed to
        // hear from the person adding them.
        var contacts = await _contactRepository.GetAllForUserAsync(request.ActorUserId, cancellationToken);
        if (contacts.All(contact => contact.ContactUserId != request.UserId))
        {
            throw new InvalidRequestException("You can only add people you already have a chat with.");
        }

        group.AddMember(request.ActorUserId, request.UserId);
        await _chatGroupRepository.UpdateAsync(group, cancellationToken);
        await NotifyAddedMemberAsync(request, group, cancellationToken);
        return true;
    }

    /// <summary>
    /// Being put into a group is the one thing that happens to a member without them doing anything, so
    /// without this it happened silently: the group simply appeared in the list, if they went looking.
    /// Best-effort, like the message notification it mirrors - a group they are already in is not undone
    /// by nobody being able to tell them about it.
    /// </summary>
    private async Task NotifyAddedMemberAsync(
        AddChatGroupMemberCommand request, ChatGroup group, CancellationToken cancellationToken)
    {
        var actor = await _userRepository.GetByIdAsync(request.ActorUserId, cancellationToken);
        if (actor is null)
        {
            return;
        }

        var payload = ChatGroupInvitationPushContent.Build(group.Id, group.Name, actor.DisplayName);
        var recordResult = await _notificationRecorder.RecordAndFilterAsync(
            request.UserId, NotificationChannel.Push, NotificationEntryKind.SharedWithYou,
            payload.Title, payload.Body, payload.Url, cancellationToken);

        if (recordResult.AllowedChannel.HasFlag(NotificationChannel.Push))
        {
            await _pushNotificationDispatcher.NotifyUserAsync(request.UserId, payload, cancellationToken);
        }
    }
}

public sealed class RemoveChatGroupMemberCommandHandler : IRequestHandler<RemoveChatGroupMemberCommand, bool>
{
    private readonly IChatGroupRepository _chatGroupRepository;

    public RemoveChatGroupMemberCommandHandler(IChatGroupRepository chatGroupRepository)
    {
        _chatGroupRepository = chatGroupRepository;
    }

    public async Task<bool> HandleAsync(RemoveChatGroupMemberCommand request, CancellationToken cancellationToken)
    {
        var group = await _chatGroupRepository.GetByIdAsync(request.GroupId, cancellationToken);
        if (group is null || !group.IsMember(request.ActorUserId))
        {
            return false;
        }

        group.RemoveMember(request.ActorUserId, request.UserId);

        // The last person out empties the group, and an empty group is not something to keep - the same
        // tidy-up DeleteAccountCommandHandler does after RemoveDeletedAccount, for the same reason.
        if (group.IsEmpty)
        {
            await _chatGroupRepository.DeleteAsync(group.Id, cancellationToken);
            return true;
        }

        await _chatGroupRepository.UpdateAsync(group, cancellationToken);
        return true;
    }
}

public sealed class ChangeChatGroupMemberRoleCommandHandler : IRequestHandler<ChangeChatGroupMemberRoleCommand, bool>
{
    private readonly IChatGroupRepository _chatGroupRepository;

    public ChangeChatGroupMemberRoleCommandHandler(IChatGroupRepository chatGroupRepository)
    {
        _chatGroupRepository = chatGroupRepository;
    }

    public async Task<bool> HandleAsync(ChangeChatGroupMemberRoleCommand request, CancellationToken cancellationToken)
    {
        var group = await _chatGroupRepository.GetByIdAsync(request.GroupId, cancellationToken);
        if (group is null || !group.IsMember(request.ActorUserId))
        {
            return false;
        }

        group.ChangeRole(request.ActorUserId, request.UserId, request.Role);
        await _chatGroupRepository.UpdateAsync(group, cancellationToken);
        return true;
    }
}
