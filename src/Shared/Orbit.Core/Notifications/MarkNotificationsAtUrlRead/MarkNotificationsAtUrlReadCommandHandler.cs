using Orbit.Core.Abstractions;
using Orbit.Core.LiveUpdates;

namespace Orbit.Core.Notifications.MarkNotificationsAtUrlRead;

public sealed class MarkNotificationsAtUrlReadCommandHandler : IRequestHandler<MarkNotificationsAtUrlReadCommand, bool>
{
    private readonly INotificationEntryRepository _notificationEntryRepository;
    private readonly ILiveUpdatePublisher _liveUpdatePublisher;

    public MarkNotificationsAtUrlReadCommandHandler(
        INotificationEntryRepository notificationEntryRepository, ILiveUpdatePublisher liveUpdatePublisher)
    {
        _notificationEntryRepository = notificationEntryRepository;
        _liveUpdatePublisher = liveUpdatePublisher;
    }

    public async Task<bool> HandleAsync(MarkNotificationsAtUrlReadCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Url))
        {
            return false;
        }

        await _notificationEntryRepository.MarkReadByUrlAsync(
            request.UserId, request.Url, DateTimeOffset.UtcNow, cancellationToken);
        await _liveUpdatePublisher.NotificationsChangedAsync(request.UserId, cancellationToken);
        return true;
    }
}
