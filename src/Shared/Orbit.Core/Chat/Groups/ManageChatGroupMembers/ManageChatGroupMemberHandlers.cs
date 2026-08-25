using Orbit.Core.Abstractions;

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

    public AddChatGroupMemberCommandHandler(IChatGroupRepository chatGroupRepository, IContactRepository contactRepository)
    {
        _chatGroupRepository = chatGroupRepository;
        _contactRepository = contactRepository;
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
        return true;
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
