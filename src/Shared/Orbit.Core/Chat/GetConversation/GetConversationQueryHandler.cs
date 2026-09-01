using Orbit.Core.Abstractions;

namespace Orbit.Core.Chat.GetConversation;

public sealed class GetConversationQueryHandler : IRequestHandler<GetConversationQuery, IReadOnlyList<ChatMessage>>
{
    private readonly IChatMessageRepository _chatMessageRepository;
    private readonly IContactRepository _contactRepository;

    public GetConversationQueryHandler(
        IChatMessageRepository chatMessageRepository, IContactRepository contactRepository)
    {
        _chatMessageRepository = chatMessageRepository;
        _contactRepository = contactRepository;
    }

    /// <summary>
    /// The conversation as this reader has it, which may begin later than it does for the other party:
    /// somebody who cleared it sees only what has been said since - see Contact.HistoryClearedAtUtc.
    ///
    /// The line is applied here rather than in the repository because it is a fact about one reader,
    /// and the repository answers the same question for both of them.
    /// </summary>
    public async Task<IReadOnlyList<ChatMessage>> HandleAsync(GetConversationQuery request, CancellationToken cancellationToken)
    {
        var contact = await _contactRepository.FindAsync(request.UserId, request.OtherUserId, cancellationToken);
        var beginsAfterUtc = Later(request.SinceUtc, contact?.HistoryClearedAtUtc);
        return await _chatMessageRepository.GetConversationAsync(
            request.UserId, request.OtherUserId, beginsAfterUtc, cancellationToken);
    }

    /// <summary>
    /// Whichever cut-off is later. The caller's own "since" is for polling; the cleared line is where
    /// this reader's conversation starts, and neither may be allowed to reach past the other.
    /// </summary>
    private static DateTimeOffset? Later(DateTimeOffset? sinceUtc, DateTimeOffset? clearedAtUtc)
        => sinceUtc is null ? clearedAtUtc
            : clearedAtUtc is null ? sinceUtc
            : sinceUtc > clearedAtUtc ? sinceUtc : clearedAtUtc;
}
