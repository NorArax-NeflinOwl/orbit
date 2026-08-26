using Orbit.Core.Users;

namespace Orbit.Core.Calendar;

/// <summary>Mirrors Orbit.Core.Notes.NoteAccessResolver - see its class comment.</summary>
public sealed class CalendarEventAccessResolver
{
    private readonly ICalendarEventRepository _calendarEventRepository;
    private readonly ICalendarEventShareRepository _calendarEventShareRepository;
    private readonly IUserRepository _userRepository;

    public CalendarEventAccessResolver(
        ICalendarEventRepository calendarEventRepository, ICalendarEventShareRepository calendarEventShareRepository, IUserRepository userRepository)
    {
        _calendarEventRepository = calendarEventRepository;
        _calendarEventShareRepository = calendarEventShareRepository;
        _userRepository = userRepository;
    }

    /// <summary>Null when callerId neither owns calendarEventId nor has an accepted share of it.</summary>
    public async Task<CalendarEvent?> ResolveAsync(Guid callerId, Guid calendarEventId, CancellationToken cancellationToken)
    {
        var ownedEvent = await _calendarEventRepository.GetByIdAsync(callerId, calendarEventId, cancellationToken);
        if (ownedEvent is not null)
        {
            var sharedOut = await _calendarEventShareRepository.GetSharedOutCalendarEventIdsAsync(callerId, cancellationToken);
            ownedEvent.SetSharedWithOthers(sharedOut.Contains(calendarEventId));
            return ownedEvent;
        }

        var grant = await _calendarEventShareRepository.FindAcceptedGrantAsync(calendarEventId, callerId, cancellationToken);
        if (grant is null)
        {
            return null;
        }

        var calendarEvent = await _calendarEventRepository.GetByIdAsync(grant.OwnerUserId, grant.SourceCalendarEventId, cancellationToken);
        if (calendarEvent is null)
        {
            return null;
        }

        var owner = await _userRepository.GetByIdAsync(grant.OwnerUserId, cancellationToken);
        calendarEvent.SetAccessContext(isShared: true, owner?.UserName, grant.AccessLevel);
        return calendarEvent;
    }

    /// <summary>Every event callerId owns, plus every event shared with them (accepted grants only).</summary>
    public async Task<IReadOnlyList<CalendarEvent>> ResolveAllAsync(Guid callerId, CancellationToken cancellationToken)
    {
        var owned = await _calendarEventRepository.GetAllAsync(callerId, cancellationToken);
        var grants = await _calendarEventShareRepository.GetAcceptedGrantsForRecipientAsync(callerId, cancellationToken);

        // Asked once for the whole list rather than per item - see GetSharedOutCalendarEventIdsAsync.
        var sharedOutIds = await _calendarEventShareRepository.GetSharedOutCalendarEventIdsAsync(callerId, cancellationToken);
        foreach (var item in owned)
        {
            item.SetSharedWithOthers(sharedOutIds.Contains(item.Id));
        }

        var granted = new List<CalendarEvent>();
        foreach (var grant in grants)
        {
            var calendarEvent = await _calendarEventRepository.GetByIdAsync(grant.OwnerUserId, grant.SourceCalendarEventId, cancellationToken);
            if (calendarEvent is null)
            {
                continue;
            }

            var owner = await _userRepository.GetByIdAsync(grant.OwnerUserId, cancellationToken);
            calendarEvent.SetAccessContext(isShared: true, owner?.UserName, grant.AccessLevel);
            granted.Add(calendarEvent);
        }

        return owned.Concat(granted).ToList();
    }
}
