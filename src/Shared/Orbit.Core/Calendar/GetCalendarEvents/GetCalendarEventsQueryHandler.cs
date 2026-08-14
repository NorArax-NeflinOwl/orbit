using Orbit.Core.Abstractions;

namespace Orbit.Core.Calendar.GetCalendarEvents;

public sealed class GetCalendarEventsQueryHandler : IRequestHandler<GetCalendarEventsQuery, IReadOnlyList<CalendarEvent>>
{
    private readonly ICalendarEventRepository _calendarEventRepository;

    public GetCalendarEventsQueryHandler(ICalendarEventRepository calendarEventRepository)
    {
        _calendarEventRepository = calendarEventRepository;
    }

    public Task<IReadOnlyList<CalendarEvent>> HandleAsync(GetCalendarEventsQuery request, CancellationToken cancellationToken)
        => _calendarEventRepository.GetAllAsync(request.UserId, cancellationToken);
}
