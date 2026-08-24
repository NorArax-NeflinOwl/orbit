using Orbit.Core.Abstractions;

namespace Orbit.Core.Notifications.GetUnreadNotificationCount;

public sealed class GetUnreadNotificationCountQueryHandler : IRequestHandler<GetUnreadNotificationCountQuery, int>
{
    private readonly INotificationEntryRepository _notificationEntryRepository;

    public GetUnreadNotificationCountQueryHandler(INotificationEntryRepository notificationEntryRepository)
    {
        _notificationEntryRepository = notificationEntryRepository;
    }

    public Task<int> HandleAsync(GetUnreadNotificationCountQuery request, CancellationToken cancellationToken)
        => _notificationEntryRepository.GetUnreadCountAsync(request.UserId, cancellationToken);
}
