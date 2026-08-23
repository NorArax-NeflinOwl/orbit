using Orbit.Core.Abstractions;

namespace Orbit.Core.Calendar.ShareCalendarEvent;

/// <summary>Mirrors Orbit.Core.Notes.ShareNote.ShareNoteCommandHandler - see its class comment for the permission rules enforced here.</summary>
public sealed class ShareCalendarEventCommandHandler : IRequestHandler<ShareCalendarEventCommand, ShareOutcome?>
{
    private readonly ICalendarEventRepository _calendarEventRepository;
    private readonly ICalendarEventShareRepository _calendarEventShareRepository;

    public ShareCalendarEventCommandHandler(
        ICalendarEventRepository calendarEventRepository, ICalendarEventShareRepository calendarEventShareRepository)
    {
        _calendarEventRepository = calendarEventRepository;
        _calendarEventShareRepository = calendarEventShareRepository;
    }

    public async Task<ShareOutcome?> HandleAsync(ShareCalendarEventCommand request, CancellationToken cancellationToken)
    {
        var sourceEvent = await _calendarEventRepository.GetByIdAsync(request.OwnerUserId, request.CalendarEventId, cancellationToken);
        if (sourceEvent is null)
        {
            return null;
        }

        var originalOwnerUserId = sourceEvent.EffectiveOwnerUserId;
        if (request.RecipientUserId == originalOwnerUserId)
        {
            return null;
        }

        if (sourceEvent.IsShared && (sourceEvent.AccessLevel < ShareAccessLevel.Share || request.AccessLevel > sourceEvent.AccessLevel))
        {
            return null;
        }

        var existingShare = await _calendarEventShareRepository.FindExistingAsync(sourceEvent.Id, request.RecipientUserId, cancellationToken);
        if (existingShare is not null)
        {
            return new ShareOutcome(existingShare.Id, AlreadyShared: true);
        }

        var share = CalendarEventShare.Create(sourceEvent.Id, request.OwnerUserId, request.RecipientUserId, originalOwnerUserId, request.AccessLevel);
        await _calendarEventShareRepository.AddAsync(share, cancellationToken);
        return new ShareOutcome(share.Id, AlreadyShared: false);
    }
}
