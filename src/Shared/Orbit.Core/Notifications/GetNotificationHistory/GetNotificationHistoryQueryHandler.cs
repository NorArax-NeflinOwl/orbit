using Orbit.Core.Abstractions;

namespace Orbit.Core.Notifications.GetNotificationHistory;

public sealed class GetNotificationHistoryQueryHandler : IRequestHandler<GetNotificationHistoryQuery, IReadOnlyList<NotificationEntry>>
{
    private readonly INotificationEntryRepository _notificationEntryRepository;

    public GetNotificationHistoryQueryHandler(INotificationEntryRepository notificationEntryRepository)
    {
        _notificationEntryRepository = notificationEntryRepository;
    }

    public Task<IReadOnlyList<NotificationEntry>> HandleAsync(GetNotificationHistoryQuery request, CancellationToken cancellationToken)
        => _notificationEntryRepository.GetHistoryAsync(request.UserId, request.Take, cancellationToken);
}
