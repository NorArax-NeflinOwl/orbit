using Orbit.Core.Abstractions;

using Orbit.Core.Notifications;

namespace Orbit.Core.Calendar.ShareCalendarEvent;

/// <summary>Mirrors Orbit.Core.Notes.ShareNote.ShareNoteCommandHandler - see its class comment for the permission rules enforced here.</summary>
public sealed class ShareCalendarEventCommandHandler : IRequestHandler<ShareCalendarEventCommand, ShareOutcome?>
{
    private readonly CalendarEventAccessResolver _calendarEventAccessResolver;
    private readonly ICalendarEventShareRepository _calendarEventShareRepository;
    private readonly ISharedItemNotifier _sharedItemNotifier;

    public ShareCalendarEventCommandHandler(
        CalendarEventAccessResolver calendarEventAccessResolver, ICalendarEventShareRepository calendarEventShareRepository, ISharedItemNotifier sharedItemNotifier)
    {
        _calendarEventAccessResolver = calendarEventAccessResolver;
        _calendarEventShareRepository = calendarEventShareRepository;
        _sharedItemNotifier = sharedItemNotifier;
    }

    public async Task<ShareOutcome?> HandleAsync(ShareCalendarEventCommand request, CancellationToken cancellationToken)
    {
        var calendarEvent = await _calendarEventAccessResolver.ResolveAsync(request.OwnerUserId, request.CalendarEventId, cancellationToken);
        if (calendarEvent is null)
        {
            return null;
        }

        if (request.RecipientUserId == calendarEvent.UserId)
        {
            return null;
        }

        if (calendarEvent.IsShared && (calendarEvent.AccessLevel < ShareAccessLevel.Share || request.AccessLevel > calendarEvent.AccessLevel))
        {
            return null;
        }

        var existingShare = await _calendarEventShareRepository.FindExistingAsync(calendarEvent.Id, request.RecipientUserId, cancellationToken);
        if (existingShare is not null)
        {
            return new ShareOutcome(existingShare.Id, AlreadyShared: true);
        }

        var share = CalendarEventShare.Create(calendarEvent.Id, calendarEvent.UserId, request.RecipientUserId, request.AccessLevel);
        await _calendarEventShareRepository.AddAsync(share, cancellationToken);
        await _sharedItemNotifier.NotifyAsync(
            request.RecipientUserId, request.OwnerUserId, SharedItemKind.CalendarEvent, calendarEvent.Details.Title, cancellationToken);
        return new ShareOutcome(share.Id, AlreadyShared: false);
    }
}
