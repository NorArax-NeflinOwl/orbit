using Orbit.Core.Abstractions;

namespace Orbit.Core.Notifications.GetUnreadNotificationEntries;

public sealed class GetUnreadNotificationEntriesQueryHandler
    : IRequestHandler<GetUnreadNotificationEntriesQuery, IReadOnlyList<NotificationEntry>>
{
    private readonly INotificationEntryRepository _notificationEntryRepository;

    public GetUnreadNotificationEntriesQueryHandler(INotificationEntryRepository notificationEntryRepository)
    {
        _notificationEntryRepository = notificationEntryRepository;
    }

    public Task<IReadOnlyList<NotificationEntry>> HandleAsync(
        GetUnreadNotificationEntriesQuery request, CancellationToken cancellationToken)
        => _notificationEntryRepository.GetUnreadAsync(request.UserId, request.Take, cancellationToken);
}
