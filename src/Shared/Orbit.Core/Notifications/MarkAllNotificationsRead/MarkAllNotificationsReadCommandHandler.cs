using Orbit.Core.Abstractions;
using Orbit.Core.LiveUpdates;

namespace Orbit.Core.Notifications.MarkAllNotificationsRead;

public sealed class MarkAllNotificationsReadCommandHandler : IRequestHandler<MarkAllNotificationsReadCommand, bool>
{
    private readonly INotificationEntryRepository _notificationEntryRepository;
    private readonly ILiveUpdatePublisher _liveUpdatePublisher;

    public MarkAllNotificationsReadCommandHandler(
        INotificationEntryRepository notificationEntryRepository, ILiveUpdatePublisher liveUpdatePublisher)
    {
        _notificationEntryRepository = notificationEntryRepository;
        _liveUpdatePublisher = liveUpdatePublisher;
    }

    public async Task<bool> HandleAsync(MarkAllNotificationsReadCommand request, CancellationToken cancellationToken)
    {
        await _notificationEntryRepository.MarkAllReadAsync(request.UserId, DateTimeOffset.UtcNow, cancellationToken);
        await _liveUpdatePublisher.NotificationsChangedAsync(request.UserId, cancellationToken);
        return true;
    }
}
