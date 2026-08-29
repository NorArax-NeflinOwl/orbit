using Microsoft.EntityFrameworkCore;
using Orbit.Core.Abstractions;
using Orbit.Core.Calendar;
using Orbit.Data.Entities;

namespace Orbit.Data.Repositories;

public sealed class CalendarEventShareRepository : ICalendarEventShareRepository
{
    private readonly OrbitDbContext _dbContext;

    public CalendarEventShareRepository(OrbitDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(CalendarEventShare share, CancellationToken cancellationToken)
    {
        _dbContext.CalendarEventShares.Add(ToEntity(share));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<CalendarEventShare?> GetByIdAsync(Guid recipientUserId, Guid id, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.CalendarEventShares
            .AsNoTracking()
            .FirstOrDefaultAsync(share => share.Id == id && share.RecipientUserId == recipientUserId, cancellationToken);

        return entity is null ? null : ToDomain(entity);
    }

    public async Task UpdateAsync(CalendarEventShare share, CancellationToken cancellationToken)
    {
        _dbContext.CalendarEventShares.Update(ToEntity(share));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> GetAcceptedRecipientUserIdsAsync(Guid sourceCalendarEventId, CancellationToken cancellationToken)
        => await _dbContext.CalendarEventShares
            .AsNoTracking()
            .Where(share => share.SourceCalendarEventId == sourceCalendarEventId && share.AcceptedAtUtc != null)
            .Select(share => share.RecipientUserId)
            .ToListAsync(cancellationToken);

    public async Task<CalendarEventShare?> FindExistingAsync(Guid sourceCalendarEventId, Guid recipientUserId, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.CalendarEventShares
            .AsNoTracking()
            .FirstOrDefaultAsync(
                share => share.SourceCalendarEventId == sourceCalendarEventId && share.RecipientUserId == recipientUserId, cancellationToken);

        return entity is null ? null : ToDomain(entity);
    }

    public async Task<CalendarEventShare?> FindAcceptedGrantAsync(Guid sourceCalendarEventId, Guid recipientUserId, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.CalendarEventShares
            .AsNoTracking()
            .FirstOrDefaultAsync(
                share => share.SourceCalendarEventId == sourceCalendarEventId && share.RecipientUserId == recipientUserId && share.AcceptedAtUtc != null,
                cancellationToken);

        return entity is null ? null : ToDomain(entity);
    }

    public async Task<IReadOnlyList<CalendarEventShare>> GetAcceptedGrantsForRecipientAsync(Guid recipientUserId, CancellationToken cancellationToken)
    {
        var entities = await _dbContext.CalendarEventShares
            .AsNoTracking()
            .Where(share => share.RecipientUserId == recipientUserId && share.AcceptedAtUtc != null)
            .ToListAsync(cancellationToken);

        return entities.Select(ToDomain).ToList();
    }

    public async Task<IReadOnlySet<Guid>> GetSharedOutCalendarEventIdsAsync(Guid ownerUserId, CancellationToken cancellationToken)
    {
        var ids = await _dbContext.CalendarEventShares
            .AsNoTracking()
            .Where(share => share.OwnerUserId == ownerUserId && share.AcceptedAtUtc != null)
            .Select(share => share.SourceCalendarEventId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return ids.ToHashSet();
    }

    private static CalendarEventShare ToDomain(CalendarEventShareEntity entity)
        => CalendarEventShare.FromPersistence(
            entity.Id, entity.SourceCalendarEventId, entity.OwnerUserId, entity.RecipientUserId,
            Enum.Parse<ShareAccessLevel>(entity.AccessLevel), entity.CreatedAtUtc, entity.AcceptedAtUtc);

    private static CalendarEventShareEntity ToEntity(CalendarEventShare share)
        => new()
        {
            Id = share.Id,
            SourceCalendarEventId = share.SourceCalendarEventId,
            OwnerUserId = share.OwnerUserId,
            RecipientUserId = share.RecipientUserId,
            AccessLevel = share.AccessLevel.ToString(),
            CreatedAtUtc = share.CreatedAtUtc,
            AcceptedAtUtc = share.AcceptedAtUtc
        };
    public async Task RemoveAcceptedGrantAsync(Guid sourceId, Guid recipientUserId, CancellationToken cancellationToken)
    {
        await _dbContext.CalendarEventShares
            .Where(share => share.SourceCalendarEventId == sourceId && share.RecipientUserId == recipientUserId && share.AcceptedAtUtc != null)
            .ExecuteDeleteAsync(cancellationToken);
    }
}