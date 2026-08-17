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
}
