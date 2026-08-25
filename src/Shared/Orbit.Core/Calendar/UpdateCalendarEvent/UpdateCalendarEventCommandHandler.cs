using Orbit.Core.Abstractions;

namespace Orbit.Core.Calendar.UpdateCalendarEvent;

public sealed class UpdateCalendarEventCommandHandler : IRequestHandler<UpdateCalendarEventCommand, EditOutcome>
{
    private readonly CalendarEventAccessResolver _calendarEventAccessResolver;
    private readonly ICalendarEventRepository _calendarEventRepository;

    public UpdateCalendarEventCommandHandler(CalendarEventAccessResolver calendarEventAccessResolver, ICalendarEventRepository calendarEventRepository)
    {
        _calendarEventAccessResolver = calendarEventAccessResolver;
        _calendarEventRepository = calendarEventRepository;
    }

    /// <summary>Mirrors Orbit.Core.Notes.UpdateNote.UpdateNoteCommandHandler - see its class comment for what NotFound/Locked mean here.</summary>
    public async Task<EditOutcome> HandleAsync(UpdateCalendarEventCommand request, CancellationToken cancellationToken)
    {
        var calendarEvent = await _calendarEventAccessResolver.ResolveAsync(request.UserId, request.Id, cancellationToken);
        if (calendarEvent is null || !calendarEvent.AccessLevel.AllowsEditing())
        {
            return EditOutcome.NotFound;
        }

        var nowUtc = DateTimeOffset.UtcNow;
        if (calendarEvent.IsLockedByAnotherUser(request.UserId, nowUtc))
        {
            return EditOutcome.LockedBy(calendarEvent.LockedByUserName!);
        }

        calendarEvent.Update(request.Details);
        await _calendarEventRepository.UpdateAsync(calendarEvent, cancellationToken);
        return EditOutcome.Success;
    }
}
