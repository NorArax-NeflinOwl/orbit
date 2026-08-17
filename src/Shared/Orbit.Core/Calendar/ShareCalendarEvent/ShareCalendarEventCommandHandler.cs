using Orbit.Core.Abstractions;

namespace Orbit.Core.Calendar.ShareCalendarEvent;

public sealed class ShareCalendarEventCommandHandler : IRequestHandler<ShareCalendarEventCommand, Guid?>
{
    private readonly ICalendarEventRepository _calendarEventRepository;
    private readonly ICalendarEventShareRepository _calendarEventShareRepository;

    public ShareCalendarEventCommandHandler(
        ICalendarEventRepository calendarEventRepository, ICalendarEventShareRepository calendarEventShareRepository)
    {
        _calendarEventRepository = calendarEventRepository;
        _calendarEventShareRepository = calendarEventShareRepository;
    }

    public async Task<Guid?> HandleAsync(ShareCalendarEventCommand request, CancellationToken cancellationToken)
    {
        var sourceEvent = await _calendarEventRepository.GetByIdAsync(request.OwnerUserId, request.CalendarEventId, cancellationToken);
        if (sourceEvent is null)
        {
            return null;
        }

        var share = CalendarEventShare.Create(sourceEvent.Id, request.OwnerUserId, request.RecipientUserId);
        await _calendarEventShareRepository.AddAsync(share, cancellationToken);
        return share.Id;
    }
}
