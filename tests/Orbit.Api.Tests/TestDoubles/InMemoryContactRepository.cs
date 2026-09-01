using Orbit.Core.Chat;

namespace Orbit.Api.Tests.TestDoubles;

/// <summary>
/// In-memory <see cref="IContactRepository"/> stub for unit tests that need real create/update-on-
/// conflict behavior without spinning up SQLite.
/// </summary>
internal sealed class InMemoryContactRepository : IContactRepository
{
    private readonly List<Contact> _contacts = [];

    public Task<IReadOnlyList<Contact>> GetAllForUserAsync(Guid ownerUserId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<Contact>>(_contacts
            .Where(contact => contact.OwnerUserId == ownerUserId)
            .OrderByDescending(contact => contact.LastMessageAtUtc)
            .ToList());

    public Task EnsureContactAsync(
        Guid ownerUserId, Guid contactUserId, DateTimeOffset lastMessageAtUtc, CancellationToken cancellationToken)
    {
        var existing = _contacts.FirstOrDefault(
            contact => contact.OwnerUserId == ownerUserId && contact.ContactUserId == contactUserId);

        if (existing is null)
        {
            _contacts.Add(Contact.Create(ownerUserId, contactUserId, lastMessageAtUtc));
        }
        else
        {
            existing.UpdateLastMessageAt(lastMessageAtUtc);
        }

        return Task.CompletedTask;
    }

    public Task<bool> SetArchivedAsync(
        Guid ownerUserId, Guid contactUserId, bool isArchived, CancellationToken cancellationToken)
    {
        var contact = Find(ownerUserId, contactUserId);
        contact?.SetArchived(isArchived);
        return Task.FromResult(contact is not null);
    }

    public Task<bool> ClearHistoryAsync(
        Guid ownerUserId, Guid contactUserId, DateTimeOffset clearedAtUtc, CancellationToken cancellationToken)
    {
        var contact = Find(ownerUserId, contactUserId);
        contact?.ClearHistory(clearedAtUtc);
        return Task.FromResult(contact is not null);
    }

    public Task<Contact?> FindAsync(Guid ownerUserId, Guid contactUserId, CancellationToken cancellationToken)
        => Task.FromResult(Find(ownerUserId, contactUserId));

    private Contact? Find(Guid ownerUserId, Guid contactUserId)
        => _contacts.FirstOrDefault(
            existing => existing.OwnerUserId == ownerUserId && existing.ContactUserId == contactUserId);
}
