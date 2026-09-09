using Orbit.Core.Abstractions;

namespace Orbit.Core.Calendar.DuplicateCalendarEvent;

public sealed class DuplicateCalendarEventCommandHandler : IRequestHandler<DuplicateCalendarEventCommand, Guid?>
{
    private readonly ICalendarEventRepository _calendarEventRepository;

    public DuplicateCalendarEventCommandHandler(ICalendarEventRepository calendarEventRepository)
    {
        _calendarEventRepository = calendarEventRepository;
    }

    /// <summary>
    /// Everything the event says - when it is, where, what colour, how it repeats and when to be
    /// reminded - with **nobody invited**. A guest list is a set of people who were asked to something;
    /// copying it would invite them all again to an appointment nobody has told them about, from a
    /// press that said "duplicate" and nothing about sending anything. The copy is the reader's own
    /// until they invite somebody to it.
    /// </summary>
    public async Task<Guid?> HandleAsync(DuplicateCalendarEventCommand request, CancellationToken cancellationToken)
    {
        if (await _calendarEventRepository.GetByIdAsync(request.UserId, request.Id, cancellationToken) is not { } calendarEvent)
        {
            return null;
        }

        var copy = CalendarEvent.Create(
            calendarEvent.UserId,
            calendarEvent.Details with
            {
                Title = request.Name ?? calendarEvent.Details.Title,
                Guests = []
            });
        await _calendarEventRepository.AddAsync(copy, cancellationToken);
        return copy.Id;
    }
}
