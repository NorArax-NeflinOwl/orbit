using Orbit.Core.Abstractions;

namespace Orbit.Core.Calendar.GetCalendarEventShareStatus;

public sealed class GetCalendarEventShareStatusQueryHandler : IRequestHandler<GetCalendarEventShareStatusQuery, bool?>
{
    private readonly ICalendarEventShareRepository _calendarEventShareRepository;

    public GetCalendarEventShareStatusQueryHandler(ICalendarEventShareRepository calendarEventShareRepository)
    {
        _calendarEventShareRepository = calendarEventShareRepository;
    }

    public async Task<bool?> HandleAsync(GetCalendarEventShareStatusQuery request, CancellationToken cancellationToken)
    {
        var share = await _calendarEventShareRepository.GetByIdAsync(request.RecipientUserId, request.ShareId, cancellationToken);
        return share?.IsAccepted;
    }
}
