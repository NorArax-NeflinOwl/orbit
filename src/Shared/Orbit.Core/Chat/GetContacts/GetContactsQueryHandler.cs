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
        // When the last message was actually sent, and only for the conversations where that can differ
        // from what the row says - see LastMessageIn. For most readers that is none of them, and the
        // query is not run at all.
        var emptiedConversations = contacts
            .Where(contact => contact.HistoryClearedAtUtc is not null)
            .Select(contact => contact.ContactUserId)
            .ToList();
        var lastMessages = await _chatMessageRepository.GetLastMessageTimesAsync(
            request.UserId, emptiedConversations, cancellationToken);

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
                otherUser, LastMessageIn(contact, lastMessages), requiresApprovalFromCurrentUser, isPendingApprovalFromOtherParty,
                unreadCounts.GetValueOrDefault(otherUser.Id), contact.IsArchived));
        }

        return summaries;
    }

    /// <summary>
    /// When this conversation last had a message this reader can still see.
    ///
    /// The Contact row's own LastMessageAtUtc is moved forward on every send and never back, so it
    /// already is the last message's time - with one exception, which is the whole of this: a reader who
    /// empties the conversation hides everything before HistoryClearedAtUtc, and the row went on saying
    /// "3 days ago" beside a conversation showing nothing at all. What a row is for is when there was
    /// last anything to read. So only an emptied conversation is asked about, and for most readers that
    /// is none of them.
    ///
    /// The cleared line is applied here rather than in the query, the division
    /// GetConversationQueryHandler makes and for the same reason: it is a fact about one reader, and the
    /// repository answers for both ends of the conversation.
    ///
    /// Falls back to the row where nothing is visible - a conversation never written in, or one emptied
    /// entirely. That is still when it was last active, which is what orders the list; a client that
    /// draws a time decides for itself whether to draw one at all.
    /// </summary>
    private static DateTimeOffset LastMessageIn(Contact contact, IReadOnlyDictionary<Guid, DateTimeOffset> lastMessages)
        => lastMessages.TryGetValue(contact.ContactUserId, out var lastSentAtUtc)
            && (contact.HistoryClearedAtUtc is not { } clearedAtUtc || lastSentAtUtc > clearedAtUtc)
                ? lastSentAtUtc
                : contact.LastMessageAtUtc;
}
