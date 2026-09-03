using Orbit.Core.Abstractions;
using Orbit.Core.LiveUpdates;

namespace Orbit.Core.Notifications.ClearNotifications;

public sealed class ClearNotificationsCommandHandler : IRequestHandler<ClearNotificationsCommand, bool>
{
    private readonly INotificationEntryRepository _notificationEntryRepository;
    private readonly ILiveUpdatePublisher _liveUpdatePublisher;

    public ClearNotificationsCommandHandler(
        INotificationEntryRepository notificationEntryRepository, ILiveUpdatePublisher liveUpdatePublisher)
    {
        _notificationEntryRepository = notificationEntryRepository;
        _liveUpdatePublisher = liveUpdatePublisher;
    }

    public async Task<bool> HandleAsync(ClearNotificationsCommand request, CancellationToken cancellationToken)
    {
        await _notificationEntryRepository.DismissAllAsync(request.UserId, DateTimeOffset.UtcNow, cancellationToken);

        // To this account, which means its other devices: clearing the panel on a phone should not
        // leave the laptop's badge lit until its next poll comes round.
        await _liveUpdatePublisher.NotificationsChangedAsync(request.UserId, cancellationToken);
        return true;
    }
}
