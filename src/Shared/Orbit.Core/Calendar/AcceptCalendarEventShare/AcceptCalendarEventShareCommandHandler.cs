using Orbit.Core.Abstractions;
using Orbit.Core.Users;

namespace Orbit.Core.Calendar.AcceptCalendarEventShare;

public sealed class AcceptCalendarEventShareCommandHandler : IRequestHandler<AcceptCalendarEventShareCommand, bool>
{
    private readonly ICalendarEventShareRepository _calendarEventShareRepository;
    private readonly ICalendarEventRepository _calendarEventRepository;
    private readonly IUserRepository _userRepository;

    public AcceptCalendarEventShareCommandHandler(
        ICalendarEventShareRepository calendarEventShareRepository,
        ICalendarEventRepository calendarEventRepository,
        IUserRepository userRepository)
    {
        _calendarEventShareRepository = calendarEventShareRepository;
        _calendarEventRepository = calendarEventRepository;
        _userRepository = userRepository;
    }

    public async Task<bool> HandleAsync(AcceptCalendarEventShareCommand request, CancellationToken cancellationToken)
    {
        var share = await _calendarEventShareRepository.GetByIdAsync(request.RecipientUserId, request.ShareId, cancellationToken);
        if (share is null)
        {
            return false;
        }

        // Already accepted - report success without creating a second calendar copy, so a duplicate
        // click (e.g. the message still shows "Akceptuj" after a page reload) is harmless.
        if (share.IsAccepted)
        {
            return true;
        }

        var sourceEvent = await _calendarEventRepository.GetByIdAsync(share.OwnerUserId, share.SourceCalendarEventId, cancellationToken);
        var owner = await _userRepository.GetByIdAsync(share.OwnerUserId, cancellationToken);
        if (sourceEvent is null || owner is null)
        {
            return false;
        }

        var sharedEvent = CalendarEvent.CreateShared(request.RecipientUserId, sourceEvent.Details, owner.UserName);
        await _calendarEventRepository.AddAsync(sharedEvent, cancellationToken);

        share.MarkAccepted(sharedEvent.Id);
        await _calendarEventShareRepository.UpdateAsync(share, cancellationToken);
        return true;
    }
}
