using Orbit.Core.Abstractions;

namespace Orbit.Core.Notifications.ClearNotifications;

public sealed class ClearNotificationsCommandHandler : IRequestHandler<ClearNotificationsCommand, bool>
{
    private readonly INotificationEntryRepository _notificationEntryRepository;

    public ClearNotificationsCommandHandler(INotificationEntryRepository notificationEntryRepository)
    {
        _notificationEntryRepository = notificationEntryRepository;
    }

    public async Task<bool> HandleAsync(ClearNotificationsCommand request, CancellationToken cancellationToken)
    {
        await _notificationEntryRepository.DeleteAllAsync(request.UserId, cancellationToken);
        return true;
    }
}
