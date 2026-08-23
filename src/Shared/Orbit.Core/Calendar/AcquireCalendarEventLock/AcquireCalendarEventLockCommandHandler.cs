using Orbit.Core.Abstractions;
using Orbit.Core.Users;

namespace Orbit.Core.Calendar.AcquireCalendarEventLock;

/// <summary>Mirrors Orbit.Core.Notes.AcquireNoteLock.AcquireNoteLockCommandHandler - see its comment.</summary>
public sealed class AcquireCalendarEventLockCommandHandler : IRequestHandler<AcquireCalendarEventLockCommand, EditOutcome>
{
    private static readonly TimeSpan LockDuration = TimeSpan.FromSeconds(60);

    private readonly CalendarEventAccessResolver _calendarEventAccessResolver;
    private readonly ICalendarEventRepository _calendarEventRepository;
    private readonly IUserRepository _userRepository;

    public AcquireCalendarEventLockCommandHandler(
        CalendarEventAccessResolver calendarEventAccessResolver, ICalendarEventRepository calendarEventRepository, IUserRepository userRepository)
    {
        _calendarEventAccessResolver = calendarEventAccessResolver;
        _calendarEventRepository = calendarEventRepository;
        _userRepository = userRepository;
    }

    public async Task<EditOutcome> HandleAsync(AcquireCalendarEventLockCommand request, CancellationToken cancellationToken)
    {
        var calendarEvent = await _calendarEventAccessResolver.ResolveAsync(request.UserId, request.CalendarEventId, cancellationToken);
        if (calendarEvent is null || calendarEvent.AccessLevel != ShareAccessLevel.CanEdit)
        {
            return EditOutcome.NotFound;
        }

        var nowUtc = DateTimeOffset.UtcNow;
        if (calendarEvent.IsLockedByAnotherUser(request.UserId, nowUtc))
        {
            return EditOutcome.LockedBy(calendarEvent.LockedByUserName!);
        }

        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        calendarEvent.AcquireLock(request.UserId, user!.UserName, nowUtc, LockDuration);
        await _calendarEventRepository.UpdateAsync(calendarEvent, cancellationToken);
        return EditOutcome.Success;
    }
}
