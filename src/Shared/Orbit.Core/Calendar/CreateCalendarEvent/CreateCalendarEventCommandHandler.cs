using Orbit.Core.Abstractions;

namespace Orbit.Core.Calendar.CreateCalendarEvent;

public sealed class CreateCalendarEventCommandHandler : IRequestHandler<CreateCalendarEventCommand, Guid>
{
    private readonly ICalendarEventRepository _calendarEventRepository;

    public CreateCalendarEventCommandHandler(ICalendarEventRepository calendarEventRepository)
    {
        _calendarEventRepository = calendarEventRepository;
    }

    public async Task<Guid> HandleAsync(CreateCalendarEventCommand request, CancellationToken cancellationToken)
    {
        var calendarEvent = CalendarEvent.Create(request.UserId, request.Details);
        await _calendarEventRepository.AddAsync(calendarEvent, cancellationToken);
        return calendarEvent.Id;
    }
}
