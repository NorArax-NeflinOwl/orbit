using Orbit.Core.Abstractions;
using Orbit.Core.LiveUpdates;

namespace Orbit.Core.Chat.Groups.CreateChatGroup;

public sealed class CreateChatGroupCommandHandler : IRequestHandler<CreateChatGroupCommand, Guid>
{
    private readonly IChatGroupRepository _chatGroupRepository;
    private readonly IContactRepository _contactRepository;
    private readonly ILiveUpdatePublisher _liveUpdatePublisher;

    public CreateChatGroupCommandHandler(
        IChatGroupRepository chatGroupRepository,
        IContactRepository contactRepository,
        ILiveUpdatePublisher liveUpdatePublisher)
    {
        _chatGroupRepository = chatGroupRepository;
        _contactRepository = contactRepository;
        _liveUpdatePublisher = liveUpdatePublisher;
    }

    public async Task<Guid> HandleAsync(CreateChatGroupCommand request, CancellationToken cancellationToken)
    {
        var group = ChatGroup.Create(request.CreatedByUserId, request.Name);

        // Only people the creator already chats with: a group is not a way to reach a stranger who never
        // agreed to hear from you, which the one-to-one side already enforces through contacts.
        var contacts = await _contactRepository.GetAllForUserAsync(request.CreatedByUserId, cancellationToken);
        var reachableUserIds = contacts.Select(contact => contact.ContactUserId).ToHashSet();

        foreach (var memberUserId in request.MemberUserIds.Distinct().Where(id => id != request.CreatedByUserId))
        {
            if (!reachableUserIds.Contains(memberUserId))
            {
                throw new InvalidRequestException("You can only add people you already have a chat with.");
            }

            group.AddMember(request.CreatedByUserId, memberUserId);
        }

        await _chatGroupRepository.AddAsync(group, cancellationToken);

        // A group appears in everybody's conversation list at once, and nobody but the creator asked
        // for it - so without this it shows up for the rest whenever their list is next read.
        await _liveUpdatePublisher.ChatChangedAsync(
            [.. group.Members.Select(member => member.UserId)], cancellationToken);
        return group.Id;
    }
}
