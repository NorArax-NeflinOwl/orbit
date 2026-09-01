using Orbit.Core.Abstractions;

namespace Orbit.Core.Calendar.CreateCalendarEvent;

/// <summary>
/// Saving a new event, and nothing else. It used to announce itself to its own owner as well - somebody
/// was told they had just made the thing they had just made. That existed to prove the notification
/// paths worked, which they now do elsewhere; the only thing worth saying when an event is saved is
/// said to somebody else, when it is shared with them (see ShareCalendarEventCommandHandler).
/// </summary>
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
