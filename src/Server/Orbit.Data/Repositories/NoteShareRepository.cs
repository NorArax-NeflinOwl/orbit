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

    public async Task<NoteShare?> FindExistingAsync(Guid sourceNoteId, Guid recipientUserId, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.NoteShares
            .AsNoTracking()
            .FirstOrDefaultAsync(share => share.SourceNoteId == sourceNoteId && share.RecipientUserId == recipientUserId, cancellationToken);

        return entity is null ? null : ToDomain(entity);
    }

    public async Task<NoteShare?> FindAcceptedGrantAsync(Guid sourceNoteId, Guid recipientUserId, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.NoteShares
            .AsNoTracking()
            .FirstOrDefaultAsync(
                share => share.SourceNoteId == sourceNoteId && share.RecipientUserId == recipientUserId && share.AcceptedAtUtc != null,
                cancellationToken);

        return entity is null ? null : ToDomain(entity);
    }

    public async Task<IReadOnlyList<NoteShare>> GetAcceptedGrantsForRecipientAsync(Guid recipientUserId, CancellationToken cancellationToken)
    {
        var entities = await _dbContext.NoteShares
            .AsNoTracking()
            .Where(share => share.RecipientUserId == recipientUserId && share.AcceptedAtUtc != null)
            .ToListAsync(cancellationToken);

        return entities.Select(ToDomain).ToList();
    }

    public async Task<IReadOnlySet<Guid>> GetSharedOutNoteIdsAsync(Guid ownerUserId, CancellationToken cancellationToken)
    {
        var noteIds = await _dbContext.NoteShares
            .AsNoTracking()
            .Where(share => share.OwnerUserId == ownerUserId && share.AcceptedAtUtc != null)
            .Select(share => share.SourceNoteId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return noteIds.ToHashSet();
    }

    private static NoteShare ToDomain(NoteShareEntity entity)
        => NoteShare.FromPersistence(
            entity.Id, entity.SourceNoteId, entity.OwnerUserId, entity.RecipientUserId,
            Enum.Parse<ShareAccessLevel>(entity.AccessLevel), entity.CreatedAtUtc, entity.AcceptedAtUtc);

    private static NoteShareEntity ToEntity(NoteShare share)
        => new()
        {
            Id = share.Id,
            SourceNoteId = share.SourceNoteId,
            OwnerUserId = share.OwnerUserId,
            RecipientUserId = share.RecipientUserId,
            AccessLevel = share.AccessLevel.ToString(),
            CreatedAtUtc = share.CreatedAtUtc,
            AcceptedAtUtc = share.AcceptedAtUtc
        };
}
