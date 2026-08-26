using Orbit.Core.Abstractions;
using Orbit.Core.Sync;

namespace Orbit.Core.Calendar.DeleteCalendarEvent;

public sealed class DeleteCalendarEventCommandHandler : IRequestHandler<DeleteCalendarEventCommand, bool>
{
    private readonly ICalendarEventRepository _calendarEventRepository;
    private readonly ICalendarEventShareRepository _calendarEventShareRepository;
    private readonly ISyncTombstoneRepository _syncTombstoneRepository;

    public DeleteCalendarEventCommandHandler(
        ICalendarEventRepository calendarEventRepository, ICalendarEventShareRepository calendarEventShareRepository,
        ISyncTombstoneRepository syncTombstoneRepository)
    {
        _calendarEventRepository = calendarEventRepository;
        _calendarEventShareRepository = calendarEventShareRepository;
        _syncTombstoneRepository = syncTombstoneRepository;
    }

    /// <summary>
    /// Deletes the caller's own event, or - when it is somebody else's, shared with them - takes it off
    /// their calendar by dropping the grant. False when it is neither, so the API answers 404 without
    /// leaking which of the two it was. Any reminder
    /// claims already recorded for this event in EventReminderDeliveries are left in place rather than
    /// cleaned up - they aren't a foreign key relationship, and EventReminderScheduler only ever finds
    /// due reminders by first looking the event up, so a deleted event simply stops producing them.
    /// </summary>
    public async Task<bool> HandleAsync(DeleteCalendarEventCommand request, CancellationToken cancellationToken)
    {
        var calendarEvent = await _calendarEventRepository.GetByIdAsync(request.UserId, request.Id, cancellationToken);
        if (calendarEvent is null)
        {
            // Not the owner's. A recipient asking to be rid of something shared with them means
            // taking it off their own list - destroying somebody else's event is not theirs to
            // do. Removing the accepted grant does exactly that and leaves the owner's untouched.
            if (await _calendarEventShareRepository.FindAcceptedGrantAsync(request.Id, request.UserId, cancellationToken) is null)
            {
                return false;
            }

            await _calendarEventShareRepository.RemoveAcceptedGrantAsync(request.Id, request.UserId, cancellationToken);
            await RecordTombstoneAsync(request, cancellationToken);
            return true;
        }

        await _calendarEventRepository.DeleteAsync(request.UserId, request.Id, cancellationToken);
        await RecordTombstoneAsync(request, cancellationToken);
        return true;
    }

    /// <summary>
    /// Tombstones are per-user, which is what lets a dropped grant leave one: the event is gone
    /// from this reader's list and from nobody else's, and that is exactly what their next delta
    /// needs to say.
    /// </summary>
    private Task RecordTombstoneAsync(DeleteCalendarEventCommand request, CancellationToken cancellationToken)
        => _syncTombstoneRepository.RecordAsync(
            new SyncTombstone(request.UserId, SyncEntityType.CalendarEvent, request.Id, DateTimeOffset.UtcNow),
            cancellationToken);
}
