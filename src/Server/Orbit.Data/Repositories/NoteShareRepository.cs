using Microsoft.EntityFrameworkCore;
using Orbit.Core.Abstractions;
using Orbit.Core.Notes;
using Orbit.Data.Entities;

namespace Orbit.Data.Repositories;

public sealed class NoteShareRepository : INoteShareRepository
{
    private readonly OrbitDbContext _dbContext;

    public NoteShareRepository(OrbitDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(NoteShare share, CancellationToken cancellationToken)
    {
        _dbContext.NoteShares.Add(ToEntity(share));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<NoteShare?> GetByIdAsync(Guid recipientUserId, Guid id, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.NoteShares
            .AsNoTracking()
            .FirstOrDefaultAsync(share => share.Id == id && share.RecipientUserId == recipientUserId, cancellationToken);

        return entity is null ? null : ToDomain(entity);
    }

    public async Task UpdateAsync(NoteShare share, CancellationToken cancellationToken)
    {
        _dbContext.NoteShares.Update(ToEntity(share));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static NoteShare ToDomain(NoteShareEntity entity)
        => NoteShare.FromPersistence(
            entity.Id, entity.SourceNoteId, entity.OwnerUserId, entity.RecipientUserId,
            Enum.Parse<ShareAccessLevel>(entity.AccessLevel), entity.CreatedAtUtc, entity.AcceptedAtUtc, entity.SharedNoteId);

    private static NoteShareEntity ToEntity(NoteShare share)
        => new()
        {
            Id = share.Id,
            SourceNoteId = share.SourceNoteId,
            OwnerUserId = share.OwnerUserId,
            RecipientUserId = share.RecipientUserId,
            AccessLevel = share.AccessLevel.ToString(),
            CreatedAtUtc = share.CreatedAtUtc,
            AcceptedAtUtc = share.AcceptedAtUtc,
            SharedNoteId = share.SharedNoteId
        };
}
