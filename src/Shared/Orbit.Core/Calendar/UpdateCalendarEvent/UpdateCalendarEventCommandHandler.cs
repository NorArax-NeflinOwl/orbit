using Orbit.Core.Abstractions;

namespace Orbit.Core.Calendar.UpdateCalendarEvent;

public sealed class UpdateCalendarEventCommandHandler : IRequestHandler<UpdateCalendarEventCommand, bool>
{
    private readonly ICalendarEventRepository _calendarEventRepository;

    public UpdateCalendarEventCommandHandler(ICalendarEventRepository calendarEventRepository)
    {
        _calendarEventRepository = calendarEventRepository;
    }

    /// <summary>
    /// Returns false instead of throwing when the event is missing or not owned by the requesting user,
    /// so the API can turn that into a 404 either way, without leaking which is the case.
    /// </summary>
    public async Task<bool> HandleAsync(UpdateCalendarEventCommand request, CancellationToken cancellationToken)
    {
        var calendarEvent = await _calendarEventRepository.GetByIdAsync(request.UserId, request.Id, cancellationToken);
        if (calendarEvent is null)
        {
            return false;
        }

        calendarEvent.Update(request.Details);
        await _calendarEventRepository.UpdateAsync(calendarEvent, cancellationToken);
        return true;
    }
}
