using Orbit.Core.Abstractions;

namespace Orbit.Core.Notifications.GetChangedNotifications;

public sealed class GetChangedNotificationsQueryHandler
    : IRequestHandler<GetChangedNotificationsQuery, IReadOnlyList<NotificationEntry>>
{
    private readonly INotificationEntryRepository _notificationEntryRepository;

    public GetChangedNotificationsQueryHandler(INotificationEntryRepository notificationEntryRepository)
    {
        _notificationEntryRepository = notificationEntryRepository;
    }

    public Task<IReadOnlyList<NotificationEntry>> HandleAsync(
        GetChangedNotificationsQuery request, CancellationToken cancellationToken)
        => _notificationEntryRepository.GetChangedSinceAsync(
            request.UserId, request.Since, request.Take, cancellationToken);
}
