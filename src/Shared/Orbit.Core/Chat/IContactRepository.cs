namespace Orbit.Core.Chat;

public interface IContactRepository
{
    /// <summary>Ordered most-recently-active conversation first.</summary>
    Task<IReadOnlyList<Contact>> GetAllForUserAsync(Guid ownerUserId, CancellationToken cancellationToken);

    /// <summary>
    /// Creates the Contact row if this is the first message between the two users, or just bumps
    /// LastMessageAtUtc if it already exists - the single operation that keeps both "shows up in the
    /// chat list" and "most recently active chat first" correct.
    /// </summary>
    Task EnsureContactAsync(Guid ownerUserId, Guid contactUserId, DateTimeOffset lastMessageAtUtc, CancellationToken cancellationToken);

    /// <summary>
    /// Puts one conversation away on this reader's list, or brings it back. Answers false when they
    /// have no row for that person - which is what an id nobody recognises looks like from here.
    ///
    /// Scoped by owner rather than by a row id, because that is the only thing the caller legitimately
    /// knows: archiving is a fact about one side's own list.
    /// </summary>
    Task<bool> SetArchivedAsync(Guid ownerUserId, Guid contactUserId, bool isArchived, CancellationToken cancellationToken);

    /// <summary>
    /// Moves this reader's own start of the conversation to clearedAtUtc - see
    /// <see cref="Contact.ClearHistory"/>. Answers false when they have no row for that person.
    /// </summary>
    Task<bool> ClearHistoryAsync(
        Guid ownerUserId, Guid contactUserId, DateTimeOffset clearedAtUtc, CancellationToken cancellationToken);

    /// <summary>One reader's row for one other person, or null - what the conversation reads to know where it begins.</summary>
    Task<Contact?> FindAsync(Guid ownerUserId, Guid contactUserId, CancellationToken cancellationToken);
}
