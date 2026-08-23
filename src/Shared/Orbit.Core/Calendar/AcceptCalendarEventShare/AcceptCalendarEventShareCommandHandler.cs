using Orbit.Core.Abstractions;

namespace Orbit.Core.Calendar.AcceptCalendarEventShare;

public sealed class AcceptCalendarEventShareCommandHandler : IRequestHandler<AcceptCalendarEventShareCommand, bool>
{
    private readonly ICalendarEventShareRepository _calendarEventShareRepository;

    public AcceptCalendarEventShareCommandHandler(ICalendarEventShareRepository calendarEventShareRepository)
    {
        _calendarEventShareRepository = calendarEventShareRepository;
    }

    /// <summary>Mirrors Orbit.Core.Notes.AcceptNoteShare.AcceptNoteShareCommandHandler - see its class comment.</summary>
    public async Task<bool> HandleAsync(AcceptCalendarEventShareCommand request, CancellationToken cancellationToken)
    {
        var share = await _calendarEventShareRepository.GetByIdAsync(request.RecipientUserId, request.ShareId, cancellationToken);
        if (share is null)
        {
            return false;
        }

        if (!share.IsAccepted)
        {
            share.MarkAccepted();
            await _calendarEventShareRepository.UpdateAsync(share, cancellationToken);
        }

        return true;
    }
}
