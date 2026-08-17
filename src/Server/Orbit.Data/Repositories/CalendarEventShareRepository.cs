using Microsoft.EntityFrameworkCore;
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

    private static CalendarEventShare ToDomain(CalendarEventShareEntity entity)
        => CalendarEventShare.FromPersistence(
            entity.Id, entity.SourceCalendarEventId, entity.OwnerUserId, entity.RecipientUserId, entity.CreatedAtUtc,
            entity.AcceptedAtUtc, entity.SharedCalendarEventId);

    private static CalendarEventShareEntity ToEntity(CalendarEventShare share)
        => new()
        {
            Id = share.Id,
            SourceCalendarEventId = share.SourceCalendarEventId,
            OwnerUserId = share.OwnerUserId,
            RecipientUserId = share.RecipientUserId,
            CreatedAtUtc = share.CreatedAtUtc,
            AcceptedAtUtc = share.AcceptedAtUtc,
            SharedCalendarEventId = share.SharedCalendarEventId
        };
}
