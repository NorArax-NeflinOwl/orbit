using Orbit.Core.Abstractions;

namespace Orbit.Core.Calendar.ArchiveCalendarEvent;

/// <summary>
/// Only the owner puts a appointment away, and only their own. A recipient must not: one row is one appointment, so
/// archiving one shared with them would take it off its owner's page - the same reason they cannot file
/// or pin one. Mirrors MoveCalendarEventToFolderCommandHandler on who may.
/// </summary>
public sealed class ArchiveCalendarEventCommandHandler : IRequestHandler<ArchiveCalendarEventCommand, bool>
{
    private readonly ICalendarEventRepository _calendarEvents;

    public ArchiveCalendarEventCommandHandler(ICalendarEventRepository calendarEvents) => _calendarEvents = calendarEvents;

    public async Task<bool> HandleAsync(ArchiveCalendarEventCommand request, CancellationToken cancellationToken)
    {
        var found = await _calendarEvents.GetByIdAsync(request.UserId, request.CalendarEventId, cancellationToken);
        if (found is null || found.UserId != request.UserId)
        {
            return false;
        }

        found.Archive(request.IsArchived);
        await _calendarEvents.UpdateAsync(found, cancellationToken);
        return true;
    }
}
