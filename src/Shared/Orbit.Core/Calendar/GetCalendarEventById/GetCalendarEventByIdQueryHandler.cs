using Orbit.Core.Abstractions;

namespace Orbit.Core.Calendar.GetCalendarEventById;

public sealed class GetCalendarEventByIdQueryHandler : IRequestHandler<GetCalendarEventByIdQuery, CalendarEvent?>
{
    private readonly ICalendarEventRepository _calendarEventRepository;

    public GetCalendarEventByIdQueryHandler(ICalendarEventRepository calendarEventRepository)
    {
        _calendarEventRepository = calendarEventRepository;
    }

    public Task<CalendarEvent?> HandleAsync(GetCalendarEventByIdQuery request, CancellationToken cancellationToken)
        => _calendarEventRepository.GetByIdAsync(request.UserId, request.Id, cancellationToken);
}
