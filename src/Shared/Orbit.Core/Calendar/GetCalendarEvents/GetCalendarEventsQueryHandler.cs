using Orbit.Core.Abstractions;

namespace Orbit.Core.Calendar.GetCalendarEvents;

public sealed class GetCalendarEventsQueryHandler : IRequestHandler<GetCalendarEventsQuery, IReadOnlyList<CalendarEvent>>
{
    private readonly CalendarEventAccessResolver _calendarEventAccessResolver;

    public GetCalendarEventsQueryHandler(CalendarEventAccessResolver calendarEventAccessResolver)
    {
        _calendarEventAccessResolver = calendarEventAccessResolver;
    }

    public Task<IReadOnlyList<CalendarEvent>> HandleAsync(GetCalendarEventsQuery request, CancellationToken cancellationToken)
        => _calendarEventAccessResolver.ResolveAllAsync(request.UserId, cancellationToken);
}
