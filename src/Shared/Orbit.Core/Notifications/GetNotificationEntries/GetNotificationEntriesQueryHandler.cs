using Orbit.Core.Abstractions;

namespace Orbit.Core.Notifications.GetNotificationEntries;

public sealed class GetNotificationEntriesQueryHandler : IRequestHandler<GetNotificationEntriesQuery, IReadOnlyList<NotificationEntry>>
{
    private readonly INotificationEntryRepository _notificationEntryRepository;

    public GetNotificationEntriesQueryHandler(INotificationEntryRepository notificationEntryRepository)
    {
        _notificationEntryRepository = notificationEntryRepository;
    }

    public Task<IReadOnlyList<NotificationEntry>> HandleAsync(GetNotificationEntriesQuery request, CancellationToken cancellationToken)
        => _notificationEntryRepository.GetRecentAsync(request.UserId, request.Take, cancellationToken);
}
