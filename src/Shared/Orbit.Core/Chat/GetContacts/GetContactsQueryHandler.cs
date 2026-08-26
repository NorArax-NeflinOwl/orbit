using Orbit.Core.Abstractions;
using Orbit.Core.Users;

namespace Orbit.Core.Chat.GetContacts;

public sealed class GetContactsQueryHandler : IRequestHandler<GetContactsQuery, IReadOnlyList<ContactSummary>>
{
    private readonly IContactRepository _contactRepository;
    private readonly IUserRepository _userRepository;
    private readonly IChatConversationAccessRepository _chatConversationAccessRepository;
    private readonly IChatMessageRepository _chatMessageRepository;

    public GetContactsQueryHandler(
        IContactRepository contactRepository, IUserRepository userRepository,
        IChatConversationAccessRepository chatConversationAccessRepository, IChatMessageRepository chatMessageRepository)
    {
        _contactRepository = contactRepository;
        _userRepository = userRepository;
        _chatConversationAccessRepository = chatConversationAccessRepository;
        _chatMessageRepository = chatMessageRepository;
    }

    /// <summary>
    /// Joins each Contact row with the other party's current profile (display name, public key) at read
    /// time rather than caching it on the Contact itself, so a changed display name or a freshly
    /// generated key pair shows up immediately. A contact whose other party's account was somehow
    /// removed is silently skipped rather than surfaced as a broken row.
    /// </summary>
    public async Task<IReadOnlyList<ContactSummary>> HandleAsync(GetContactsQuery request, CancellationToken cancellationToken)
    {
        var contacts = await _contactRepository.GetAllForUserAsync(request.UserId, cancellationToken);
        // Counted from the messages themselves rather than from the notification feed: clearing
        // notifications is tidying, not reading, and a conversation the reader has not opened stays
        // unread however often they clear the panel.
        var unreadCounts = await _chatMessageRepository.GetUnreadCountsBySenderAsync(request.UserId, cancellationToken);

        // Three queries for the whole list rather than two per contact. This loop used to ask for each
        // other party's profile and each conversation's access state one at a time, so a reader with
        // thirty contacts cost sixty round trips - on a list the chat window refreshes on every poll.
        var contactUserIds = contacts.Select(contact => contact.ContactUserId).ToList();
        var usersById = (await _userRepository.GetByIdsAsync(contactUserIds, cancellationToken))
            .ToDictionary(user => user.Id);
        var accessByOtherParty = await _chatConversationAccessRepository.GetAllForUserAsync(request.UserId, cancellationToken);

        var summaries = new List<ContactSummary>(contacts.Count);
        foreach (var contact in contacts)
        {
            if (!usersById.TryGetValue(contact.ContactUserId, out var otherUser))
            {
                continue;
            }

            var access = accessByOtherParty.GetValueOrDefault(contact.ContactUserId);
            var requiresApprovalFromCurrentUser = access is { IsApproved: false } && access.InitiatedByUserId != request.UserId;
            var isPendingApprovalFromOtherParty = access is { IsApproved: false } && access.InitiatedByUserId == request.UserId;
            summaries.Add(new ContactSummary(
                otherUser, contact.LastMessageAtUtc, requiresApprovalFromCurrentUser, isPendingApprovalFromOtherParty,
                unreadCounts.GetValueOrDefault(otherUser.Id)));
        }

        return summaries;
    }
}
