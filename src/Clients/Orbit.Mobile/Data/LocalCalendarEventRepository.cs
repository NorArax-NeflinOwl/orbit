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
public sealed class LocalCalendarEventRepository : ICopyReviewStore
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
    /// - see <see cref="PendingCalendarLink"/>. Keyed on the event, so correcting an appointment before
    /// it syncs updates the one pairing rather than making a second.
    /// </summary>
    public async Task RememberPendingLinkAsync(
        Guid calendarEventLocalId, Guid taskListLocalId, string description,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (await dbContext.PendingCalendarLinks.FirstOrDefaultAsync(
                link => link.CalendarEventLocalId == calendarEventLocalId, cancellationToken) is { } existing)
        {
            existing.TaskListLocalId = taskListLocalId;
            existing.Description = description;
        }
        else
        {
            dbContext.PendingCalendarLinks.Add(new PendingCalendarLink
            {
                CalendarEventLocalId = calendarEventLocalId,
                TaskListLocalId = taskListLocalId,
                Description = description
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// The event an entry stands for while it is still waiting to be named, or null when it is not
    /// waiting for one. What lets an appointment made offline be reopened and corrected rather than
    /// showing an empty form that would make a second event on the next save.
    ///
    /// Found by the words the entry carries, for the reason <see cref="PendingCalendarLink"/> gives: an
    /// entry made offline has no id to be found by.
    /// </summary>
    public async Task<LocalCalendarEvent?> FindPendingForAsync(
        Guid taskListLocalId, string description, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (await dbContext.PendingCalendarLinks.AsNoTracking().FirstOrDefaultAsync(
                link => link.TaskListLocalId == taskListLocalId && link.Description == description,
                cancellationToken) is not { } link)
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

        // A copy still awaiting review is written to this phone and queued for nobody: what it is has
        // not been decided yet, and the review is what sends it - see LocalNoteRepository.UpdateAsync.
        if (!CopiesForEditing.IsAwaitingReview(calendarEvent))
        {
            Enqueue(dbContext, localId, OutboxOperation.Update, now);
        }
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

    /// <summary>
    /// <inheritdoc cref="LocalNoteRepository.CopyForEditingAsync" path="/summary/node()[1]"/>
    ///
    /// An appointment has no sealed form of its own - everything it is travels as one block the server
    /// stores whole - so the only reason to refuse is that there is nothing there to copy.
    /// </summary>
    public async Task<LocalCalendarEvent?> CopyForEditingAsync(
        Guid originalLocalId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (await dbContext.CalendarEvents.FirstOrDefaultAsync(
                candidate => candidate.LocalId == originalLocalId, cancellationToken) is not { } original)
        {
            return null;
        }

        var now = _timeProvider.GetUtcNow();
        var copy = new LocalCalendarEvent
        {
            LocalId = Guid.NewGuid(),
            ServerId = null,
            Details = original.Details,
            CopyOfLocalId = original.LocalId,
            CopiedAtUtc = now,
            CopyBaseTitle = original.Details.Title,
            CopyBaseLines = Describe(original.Details),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        dbContext.CalendarEvents.Add(copy);
        CopiesForEditing.Announce(
            dbContext, CopyKind.CalendarEvent, copy.LocalId, original.Details.Title, now);
        await dbContext.SaveChangesAsync(cancellationToken);
        return copy;
    }

    public CopyKind Kind => CopyKind.CalendarEvent;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CopyUnderReview>> GetCopiesAwaitingReviewAsync(
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await DescribeAllAsync(dbContext, CopiesForEditing.AwaitingReviewAsync<LocalCalendarEvent>, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CopyUnderReview>> GetKeptCopiesAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await DescribeAllAsync(dbContext, CopiesForEditing.KeptAsync<LocalCalendarEvent>, cancellationToken);
    }

    /// <inheritdoc cref="LocalNoteRepository.ApplyCopyAsync"/>
    public async Task<LocalWriteOutcome> ApplyCopyAsync(Guid copyLocalId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (await CopiesForEditing.FindCopyAsync<LocalCalendarEvent>(dbContext, copyLocalId, cancellationToken)
            is not { CopyOfLocalId: { } originalLocalId } copy)
        {
            return LocalWriteOutcome.NotFound;
        }

        if (await dbContext.CalendarEvents.FirstOrDefaultAsync(
                candidate => candidate.LocalId == originalLocalId, cancellationToken) is not { } original)
        {
            return LocalWriteOutcome.NotFound;
        }

        if (!OfflineEditPolicy.IsAllowed(original, _networkStatus))
        {
            return LocalWriteOutcome.RefusedWhileOffline;
        }

        var now = _timeProvider.GetUtcNow();
        original.Details = copy.Details;
        original.UpdatedAtUtc = now;
        Enqueue(dbContext, original.LocalId, OutboxOperation.Update, now, original.ServerId);

        CopiesForEditing.Remove(dbContext, copy, SyncEntityType.CalendarEvent);
        await dbContext.SaveChangesAsync(cancellationToken);
        return LocalWriteOutcome.Applied;
    }

    /// <inheritdoc/>
    public async Task<LocalWriteOutcome> DiscardCopyAsync(Guid copyLocalId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (await CopiesForEditing.FindCopyAsync<LocalCalendarEvent>(dbContext, copyLocalId, cancellationToken)
            is not { } copy)
        {
            return LocalWriteOutcome.NotFound;
        }

        CopiesForEditing.Remove(dbContext, copy, SyncEntityType.CalendarEvent);
        await dbContext.SaveChangesAsync(cancellationToken);
        return LocalWriteOutcome.Applied;
    }

    /// <inheritdoc/>
    public async Task<LocalWriteOutcome> KeepCopyAsync(Guid copyLocalId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (await CopiesForEditing.FindCopyAsync<LocalCalendarEvent>(dbContext, copyLocalId, cancellationToken)
            is not { } copy)
        {
            return LocalWriteOutcome.NotFound;
        }

        // Nothing inside an appointment carries an id of its own, so there is nothing here to re-issue.
        var now = _timeProvider.GetUtcNow();
        copy.UpdatedAtUtc = now;
        CopiesForEditing.Keep(dbContext, copy, SyncEntityType.CalendarEvent, now);
        await dbContext.SaveChangesAsync(cancellationToken);
        return LocalWriteOutcome.Applied;
    }

    /// <inheritdoc cref="LocalNoteRepository.DescribeAllAsync"/>
    private async Task<IReadOnlyList<CopyUnderReview>> DescribeAllAsync(
        OrbitLocalDbContext dbContext,
        Func<OrbitLocalDbContext, CancellationToken, Task<IReadOnlyList<LocalCalendarEvent>>> read,
        CancellationToken cancellationToken)
    {
        var described = new List<CopyUnderReview>();
        foreach (var copy in await read(dbContext, cancellationToken))
        {
            var original = await dbContext.CalendarEvents.AsNoTracking().FirstOrDefaultAsync(
                candidate => candidate.LocalId == copy.CopyOfLocalId, cancellationToken);

            described.Add(new CopyUnderReview(
                CopyKind.CalendarEvent, copy.LocalId, copy.CopyOfLocalId!.Value,
                original?.Details.Title is { Length: > 0 } title ? title : copy.CopyBaseTitle,
                copy.CopiedAtUtc ?? copy.CreatedAtUtc,
                copy.CopyBaseLines, Describe(copy.Details),
                original is null ? null : Describe(original.Details)));
        }

        return described;
    }

    /// <summary>
    /// An appointment as a review reads it: what it is called, when it is, where, and what it says.
    /// Times are written plainly rather than in the reader's own format - see
    /// <see cref="LocalTaskListRepository.Describe"/> for why that matters here.
    /// </summary>
    private static IReadOnlyList<string> Describe(CalendarEventDetailsDto details)
    {
        var lines = new List<string> { details.Title };

        lines.Add(details.IsAllDay
            ? $"{details.StartUtc:yyyy-MM-dd} - {details.EndUtc:yyyy-MM-dd}"
            : $"{details.StartUtc:yyyy-MM-dd HH:mm} - {details.EndUtc:yyyy-MM-dd HH:mm}");

        if (details.Location?.Address is { Length: > 0 } place)
        {
            lines.Add(place);
        }

        if (details.Description is { Length: > 0 } description)
        {
            lines.AddRange(description.Split('\n'));
        }

        return lines;
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
