using Orbit.Core.Abstractions;
using Orbit.Core.Users;

namespace Orbit.Core.Chat.GetContacts;

public sealed class GetContactsQueryHandler : IRequestHandler<GetContactsQuery, IReadOnlyList<ContactSummary>>
{
    private readonly IContactRepository _contactRepository;
    private readonly IUserRepository _userRepository;
    private readonly IChatConversationAccessRepository _chatConversationAccessRepository;

    public GetContactsQueryHandler(
        IContactRepository contactRepository, IUserRepository userRepository,
        IChatConversationAccessRepository chatConversationAccessRepository)
    {
        _contactRepository = contactRepository;
        _userRepository = userRepository;
        _chatConversationAccessRepository = chatConversationAccessRepository;
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
        var summaries = new List<ContactSummary>();

        foreach (var contact in contacts)
        {
            var otherUser = await _userRepository.GetByIdAsync(contact.ContactUserId, cancellationToken);
            if (otherUser is null)
            {
                continue;
            }

            var access = await _chatConversationAccessRepository.GetAsync(request.UserId, contact.ContactUserId, cancellationToken);
            var requiresApprovalFromCurrentUser = access is { IsApproved: false } && access.InitiatedByUserId != request.UserId;
            summaries.Add(new ContactSummary(otherUser, contact.LastMessageAtUtc, requiresApprovalFromCurrentUser));
        }

        return summaries;
    }
}
