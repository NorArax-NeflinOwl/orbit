using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orbit.Contracts.Tasks;
using Orbit.Core.Sync;
using Orbit.Core.Tasks;
using Orbit.Mobile.Data;

namespace Orbit.Mobile.Sync;

/// <summary>
/// Finishes the appointments that were made with no connection.
///
/// A Calendar task entry carries its event's <b>server</b> id, and an event created offline has none
/// until the calendar's outbox flushes. Until then the pairing lives in
/// <see cref="PendingCalendarLink"/>; this is what turns it into the real link once the server has
/// named the event, and queues the list so the entry's new id goes up too.
///
/// Run from inside <see cref="CalendarEventSynchronizer"/> rather than beside it, deliberately: the
/// moment an event gains a server id is the moment this has to happen, and a caller that forgot to run
/// it would leave an entry pointing at nothing while everything looked synchronised.
/// </summary>
public sealed class PendingCalendarLinkResolver
{
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PendingCalendarLinkResolver> _logger;

    public PendingCalendarLinkResolver(
        TimeProvider timeProvider, ILogger<PendingCalendarLinkResolver> logger)
    {
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>
    /// Fills in every link whose event now has a server id, and returns how many were finished.
    ///
    /// Takes the caller's own <see cref="OrbitLocalDbContext"/> so the whole thing happens in one place
    /// with the push that produced the ids - the same context the outbox replay just used.
    /// </summary>
    public async Task<int> ResolveAsync(OrbitLocalDbContext dbContext, CancellationToken cancellationToken)
    {
        var waiting = await dbContext.PendingCalendarLinks.ToListAsync(cancellationToken);
        if (waiting.Count == 0)
        {
            return 0;
        }

        var named = await dbContext.CalendarEvents
            .Where(calendarEvent => calendarEvent.ServerId != null)
            .ToDictionaryAsync(
                calendarEvent => calendarEvent.LocalId, calendarEvent => calendarEvent.ServerId!.Value, cancellationToken);

        var resolved = 0;
        foreach (var link in waiting)
        {
            if (!named.TryGetValue(link.CalendarEventLocalId, out var eventServerId))
            {
                continue;
            }

            // The event was named but its list is gone - the entry it belonged to went with it, so
            // there is nothing left to point anywhere and the row is only litter.
            if (await dbContext.TaskLists.FirstOrDefaultAsync(
                    list => list.LocalId == link.TaskListLocalId, cancellationToken) is not { } taskList)
            {
                dbContext.PendingCalendarLinks.Remove(link);
                continue;
            }

            if (!TryPointAtTheEvent(taskList, link.Description, eventServerId, out var items))
            {
                dbContext.PendingCalendarLinks.Remove(link);
                continue;
            }

            taskList.Items = items;
            taskList.UpdatedAtUtc = _timeProvider.GetUtcNow();
            // Queued, not just stored: the entry now carries an id the server has never seen on it, and
            // without a push the appointment would be linked here and nowhere else.
            dbContext.Outbox.Add(new OutboxEntry
            {
                EntityType = SyncEntityType.TaskList,
                LocalId = taskList.LocalId,
                ServerId = taskList.ServerId,
                Operation = OutboxOperation.Update,
                QueuedAtUtc = _timeProvider.GetUtcNow()
            });

            dbContext.PendingCalendarLinks.Remove(link);
            resolved++;
        }

        if (resolved > 0)
        {
            _logger.LogInformation("{Count} appointment(s) made offline now carry the id the server gave them", resolved);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return resolved;
    }

    /// <summary>
    /// Finds the entry the appointment was made for and points it at the event.
    ///
    /// By the words rather than by an id, for the reason <see cref="PendingCalendarLink"/> gives: an
    /// entry made offline is given its id by the server, so the id it had when the appointment was made
    /// is not the id it has now. An appointment that already has one is skipped, which is what keeps two
    /// appointments made on one list from both landing on the same entry.
    ///
    /// False when there is no such entry - deleted, or turned back into an errand while the phone was
    /// offline. The link is then stale rather than pending, and the event stays in the calendar for the
    /// reader to deal with, exactly as it does when the same thing happens online.
    /// </summary>
    private static bool TryPointAtTheEvent(
        LocalTaskList taskList, string description, Guid eventServerId, out IReadOnlyList<TaskItemDto> items)
    {
        items = taskList.Items;
        if (taskList.Items.FirstOrDefault(item =>
                item.Kind == nameof(TaskItemKind.Calendar)
                && item.LinkedCalendarEventId is null
                && item.Description == description) is not { } waiting)
        {
            return false;
        }

        items =
        [
            .. taskList.Items.Select(item => ReferenceEquals(item, waiting)
                ? item with { LinkedCalendarEventId = eventServerId }
                : item)
        ];

        return true;
    }
}
