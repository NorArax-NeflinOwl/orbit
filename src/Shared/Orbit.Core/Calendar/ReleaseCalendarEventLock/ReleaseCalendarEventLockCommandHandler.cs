using Orbit.Core.Abstractions;

namespace Orbit.Core.Calendar.ReleaseCalendarEventLock;

public sealed class ReleaseCalendarEventLockCommandHandler : IRequestHandler<ReleaseCalendarEventLockCommand, bool>
{
    private readonly CalendarEventAccessResolver _calendarEventAccessResolver;
    private readonly ICalendarEventRepository _calendarEventRepository;

    public ReleaseCalendarEventLockCommandHandler(CalendarEventAccessResolver calendarEventAccessResolver, ICalendarEventRepository calendarEventRepository)
    {
        _calendarEventAccessResolver = calendarEventAccessResolver;
        _calendarEventRepository = calendarEventRepository;
    }

    public async Task<bool> HandleAsync(ReleaseCalendarEventLockCommand request, CancellationToken cancellationToken)
    {
        var calendarEvent = await _calendarEventAccessResolver.ResolveAsync(request.UserId, request.CalendarEventId, cancellationToken);
        if (calendarEvent is null)
        {
            return false;
        }

        calendarEvent.ReleaseLock(request.UserId);
        await _calendarEventRepository.UpdateAsync(calendarEvent, cancellationToken);
        return true;
    }
}
