using Microsoft.EntityFrameworkCore;
using Orbit.Contracts.Calendar;
using Orbit.Core.Sync;
using Orbit.Mobile.Sync;

namespace Orbit.Mobile.Data;

/// <summary>
/// Every read and write a screen performs on calendar events. The same shape as the note and task-list
/// repositories, including the rule that matters: each write records its own outbox entry in the same
/// transaction as the change, and the offline policy is refused here rather than only shown on screen.
/// </summary>
public sealed class LocalCalendarEventRepository
{
    private readonly IDbContextFactory<OrbitLocalDbContext> _dbContextFactory;
    private readonly TimeProvider _timeProvider;
    private readonly INetworkStatus _networkStatus;

    public LocalCalendarEventRepository(
        IDbContextFactory<OrbitLocalDbContext> dbContextFactory, TimeProvider timeProvider, INetworkStatus networkStatus)
    {
        _dbContextFactory = dbContextFactory;
        _timeProvider = timeProvider;
        _networkStatus = networkStatus;
    }

    /// <summary>Soonest first, which is the only order a calendar is ever read in.</summary>
    public async Task<IReadOnlyList<LocalCalendarEvent>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var events = await dbContext.CalendarEvents.AsNoTracking().ToListAsync(cancellationToken);

        // Ordered here rather than in SQL: the start time lives inside the JSON block, which SQLite
        // cannot sort on without picking it apart. A phone's calendar is small enough for that to cost
        // nothing, and pulling the column out purely to sort on it would duplicate the truth.
        return events.OrderBy(calendarEvent => calendarEvent.Details.StartUtc).ToList();
    }

    public async Task<LocalCalendarEvent?> FindAsync(Guid localId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.CalendarEvents.AsNoTracking()
            .FirstOrDefaultAsync(calendarEvent => calendarEvent.LocalId == localId, cancellationToken);
    }

    /// <summary>Whether this event may be changed right now, without changing it.</summary>
    public async Task<bool> CanEditAsync(Guid localId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var calendarEvent = await dbContext.CalendarEvents.AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.LocalId == localId, cancellationToken);

        return calendarEvent is not null && OfflineEditPolicy.IsAllowed(calendarEvent, _networkStatus);
    }

    public async Task<IReadOnlySet<Guid>> GetPendingLocalIdsAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var localIds = await dbContext.Outbox
            .Where(entry => entry.EntityType == SyncEntityType.CalendarEvent)
            .Select(entry => entry.LocalId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return localIds.ToHashSet();
    }

    public async Task<LocalCalendarEvent> CreateAsync(
        CalendarEventDetailsDto details, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = _timeProvider.GetUtcNow();
        var calendarEvent = new LocalCalendarEvent
        {
            LocalId = Guid.NewGuid(),
            ServerId = null,
            Details = details,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        dbContext.CalendarEvents.Add(calendarEvent);
        Enqueue(dbContext, calendarEvent.LocalId, OutboxOperation.Create, now);
        await dbContext.SaveChangesAsync(cancellationToken);
        return calendarEvent;
    }

    /// <summary>
    /// Remembers that a task entry stands for an event this phone made and the server has not named yet
    /// - see <see cref="PendingCalendarLink"/>. Replaces any earlier pairing for the same entry, so
    /// saving an appointment twice before it syncs corrects the one event rather than making a second.
    /// </summary>
    public async Task RememberPendingLinkAsync(
        Guid taskItemId, Guid taskListLocalId, Guid calendarEventLocalId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (await dbContext.PendingCalendarLinks.FirstOrDefaultAsync(
                link => link.TaskItemId == taskItemId, cancellationToken) is { } existing)
        {
            existing.TaskListLocalId = taskListLocalId;
            existing.CalendarEventLocalId = calendarEventLocalId;
        }
        else
        {
            dbContext.PendingCalendarLinks.Add(new PendingCalendarLink
            {
                TaskItemId = taskItemId,
                TaskListLocalId = taskListLocalId,
                CalendarEventLocalId = calendarEventLocalId
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// The event an entry stands for while it is still waiting to be named, or null when it is not
    /// waiting for one. What lets an appointment made offline be reopened and corrected rather than
    /// showing an empty form that would make a second event on the next save.
    /// </summary>
    public async Task<LocalCalendarEvent?> FindPendingForTaskItemAsync(
        Guid taskItemId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (await dbContext.PendingCalendarLinks.AsNoTracking().FirstOrDefaultAsync(
                link => link.TaskItemId == taskItemId, cancellationToken) is not { } link)
        {
            return null;
        }

        return await dbContext.CalendarEvents.AsNoTracking()
            .FirstOrDefaultAsync(calendarEvent => calendarEvent.LocalId == link.CalendarEventLocalId, cancellationToken);
    }

    /// <summary>Refuses rather than queues when the offline policy forbids it - see LocalWriteOutcome.</summary>
    public async Task<LocalWriteOutcome> UpdateAsync(
        Guid localId, CalendarEventDetailsDto details, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (await dbContext.CalendarEvents.FirstOrDefaultAsync(
                candidate => candidate.LocalId == localId, cancellationToken) is not { } calendarEvent)
        {
            return LocalWriteOutcome.NotFound;
        }

        if (!OfflineEditPolicy.IsAllowed(calendarEvent, _networkStatus))
        {
            return LocalWriteOutcome.RefusedWhileOffline;
        }

        var now = _timeProvider.GetUtcNow();
        calendarEvent.Details = details;
        calendarEvent.UpdatedAtUtc = now;

        Enqueue(dbContext, localId, OutboxOperation.Update, now);
        await dbContext.SaveChangesAsync(cancellationToken);
        return LocalWriteOutcome.Applied;
    }

    public async Task<LocalWriteOutcome> DeleteAsync(Guid localId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (await dbContext.CalendarEvents.FirstOrDefaultAsync(
                candidate => candidate.LocalId == localId, cancellationToken) is not { } calendarEvent)
        {
            return LocalWriteOutcome.NotFound;
        }

        if (!OfflineEditPolicy.IsAllowed(calendarEvent, _networkStatus))
        {
            return LocalWriteOutcome.RefusedWhileOffline;
        }

        dbContext.CalendarEvents.Remove(calendarEvent);

        // An event the server never saw has nothing to delete there, and dropping what was queued for it
        // stops replay creating the event the user has just thrown away.
        if (calendarEvent.ServerId is null)
        {
            dbContext.Outbox.RemoveRange(dbContext.Outbox.Where(
                entry => entry.EntityType == SyncEntityType.CalendarEvent && entry.LocalId == localId));
        }
        else
        {
            Enqueue(dbContext, localId, OutboxOperation.Delete, _timeProvider.GetUtcNow(), calendarEvent.ServerId);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return LocalWriteOutcome.Applied;
    }

    private static void Enqueue(
        OrbitLocalDbContext dbContext, Guid localId, OutboxOperation operation, DateTimeOffset queuedAtUtc,
        Guid? serverId = null)
        => dbContext.Outbox.Add(new OutboxEntry
        {
            EntityType = SyncEntityType.CalendarEvent,
            LocalId = localId,
            ServerId = serverId,
            Operation = operation,
            QueuedAtUtc = queuedAtUtc
        });
}
