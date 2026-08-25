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

        if (calendarEvent.IsShared && !calendarEvent.AccessLevel.CanGrant(request.AccessLevel))
        {
            return null;
        }

        var existingShare = await _calendarEventShareRepository.FindExistingAsync(calendarEvent.Id, request.RecipientUserId, cancellationToken);
        if (existingShare is not null)
        {
            // Sharing again at a higher level raises the existing offer rather than being a no-op:
            // that is how an owner answers a request for edit access (see RequestEditAccess), and
            // "share it with them again, but with more" is what they mean by doing it.
            var accessLevelRaised = existingShare.RaiseAccessLevelTo(request.AccessLevel);
            if (accessLevelRaised)
            {
                await _calendarEventShareRepository.UpdateAsync(existingShare, cancellationToken);
            }

            return new ShareOutcome(existingShare.Id, AlreadyShared: true, accessLevelRaised);
        }

        var share = CalendarEventShare.Create(calendarEvent.Id, calendarEvent.UserId, request.RecipientUserId, request.AccessLevel);
        await _calendarEventShareRepository.AddAsync(share, cancellationToken);
        await _sharedItemNotifier.NotifyAsync(
            request.RecipientUserId, request.OwnerUserId, SharedItemKind.CalendarEvent, calendarEvent.Details.Title, cancellationToken);
        return new ShareOutcome(share.Id, AlreadyShared: false);
    }
}
