using Orbit.Core.Abstractions;

namespace Orbit.Core.Calendar.DeleteCalendarEvent;

public sealed class DeleteCalendarEventCommandHandler : IRequestHandler<DeleteCalendarEventCommand, bool>
{
    private readonly ICalendarEventRepository _calendarEventRepository;

    public DeleteCalendarEventCommandHandler(ICalendarEventRepository calendarEventRepository)
    {
        _calendarEventRepository = calendarEventRepository;
    }

    /// <summary>
    /// Returns false instead of throwing when the event is missing or not owned by the requesting user,
    /// so the API can turn that into a 404 either way, without leaking which is the case. Any reminder
    /// claims already recorded for this event in EventReminderDeliveries are left in place rather than
    /// cleaned up - they aren't a foreign key relationship, and EventReminderScheduler only ever finds
    /// due reminders by first looking the event up, so a deleted event simply stops producing them.
    /// </summary>
    public async Task<bool> HandleAsync(DeleteCalendarEventCommand request, CancellationToken cancellationToken)
    {
        var calendarEvent = await _calendarEventRepository.GetByIdAsync(request.UserId, request.Id, cancellationToken);
        if (calendarEvent is null)
        {
            return false;
        }

        await _calendarEventRepository.DeleteAsync(request.UserId, request.Id, cancellationToken);
        return true;
    }
}
