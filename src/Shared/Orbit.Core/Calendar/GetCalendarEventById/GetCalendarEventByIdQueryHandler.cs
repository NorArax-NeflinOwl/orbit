using Orbit.Core.Abstractions;

namespace Orbit.Core.Calendar.GetCalendarEventById;

public sealed class GetCalendarEventByIdQueryHandler : IRequestHandler<GetCalendarEventByIdQuery, CalendarEvent?>
{
    private readonly CalendarEventAccessResolver _calendarEventAccessResolver;

    public GetCalendarEventByIdQueryHandler(CalendarEventAccessResolver calendarEventAccessResolver)
    {
        _calendarEventAccessResolver = calendarEventAccessResolver;
    }

    public Task<CalendarEvent?> HandleAsync(GetCalendarEventByIdQuery request, CancellationToken cancellationToken)
        => _calendarEventAccessResolver.ResolveAsync(request.UserId, request.Id, cancellationToken);
}
