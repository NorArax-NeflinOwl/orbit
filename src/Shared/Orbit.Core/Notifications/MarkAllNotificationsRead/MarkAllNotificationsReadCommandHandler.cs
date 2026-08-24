using Orbit.Core.Abstractions;

namespace Orbit.Core.Notifications.MarkAllNotificationsRead;

public sealed class MarkAllNotificationsReadCommandHandler : IRequestHandler<MarkAllNotificationsReadCommand, bool>
{
    private readonly INotificationEntryRepository _notificationEntryRepository;

    public MarkAllNotificationsReadCommandHandler(INotificationEntryRepository notificationEntryRepository)
    {
        _notificationEntryRepository = notificationEntryRepository;
    }

    public async Task<bool> HandleAsync(MarkAllNotificationsReadCommand request, CancellationToken cancellationToken)
    {
        await _notificationEntryRepository.MarkAllReadAsync(request.UserId, DateTimeOffset.UtcNow, cancellationToken);
        return true;
    }
}
