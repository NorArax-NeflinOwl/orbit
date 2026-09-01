using Microsoft.EntityFrameworkCore;
using Orbit.Core.Chat;
using Orbit.Data.Entities;

namespace Orbit.Data.Repositories;

public sealed class ContactRepository : IContactRepository
{
    private readonly OrbitDbContext _dbContext;

    public ContactRepository(OrbitDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<Contact>> GetAllForUserAsync(Guid ownerUserId, CancellationToken cancellationToken)
    {
        // SQLite can't translate ORDER BY on a DateTimeOffset column, so the sort has to happen in
        // memory after fetching (see the EF Core NotSupportedException this avoids).
        var entities = await _dbContext.Contacts
            .AsNoTracking()
            .Where(entity => entity.OwnerUserId == ownerUserId)
            .ToListAsync(cancellationToken);

        return entities
            .OrderByDescending(entity => entity.LastMessageAtUtc)
            .Select(ToDomain)
            .ToList();
    }

    public async Task EnsureContactAsync(
        Guid ownerUserId, Guid contactUserId, DateTimeOffset lastMessageAtUtc, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Contacts
            .FirstOrDefaultAsync(contact => contact.OwnerUserId == ownerUserId && contact.ContactUserId == contactUserId, cancellationToken);

        if (entity is null)
        {
            _dbContext.Contacts.Add(ToEntity(Contact.Create(ownerUserId, contactUserId, lastMessageAtUtc)));
        }
        else
        {
            entity.LastMessageAtUtc = lastMessageAtUtc;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static Contact ToDomain(ContactEntity entity)
        => Contact.FromPersistence(
            entity.Id, entity.OwnerUserId, entity.ContactUserId, entity.CreatedAtUtc, entity.LastMessageAtUtc,
            entity.IsArchived);

    private static ContactEntity ToEntity(Contact contact)
        => new()
        {
            Id = contact.Id,
            OwnerUserId = contact.OwnerUserId,
            ContactUserId = contact.ContactUserId,
            CreatedAtUtc = contact.CreatedAtUtc,
            LastMessageAtUtc = contact.LastMessageAtUtc,
            IsArchived = contact.IsArchived
        };
}
