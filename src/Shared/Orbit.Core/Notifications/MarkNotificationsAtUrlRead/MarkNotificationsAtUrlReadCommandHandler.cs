using Orbit.Core.Abstractions;

namespace Orbit.Core.Notifications.MarkNotificationsAtUrlRead;

public sealed class MarkNotificationsAtUrlReadCommandHandler : IRequestHandler<MarkNotificationsAtUrlReadCommand, bool>
{
    private readonly INotificationEntryRepository _notificationEntryRepository;

    public MarkNotificationsAtUrlReadCommandHandler(INotificationEntryRepository notificationEntryRepository)
    {
        _notificationEntryRepository = notificationEntryRepository;
    }

    public async Task<bool> HandleAsync(MarkNotificationsAtUrlReadCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Url))
        {
            return false;
        }

        await _notificationEntryRepository.MarkReadByUrlAsync(
            request.UserId, request.Url, DateTimeOffset.UtcNow, cancellationToken);
        return true;
    }
}
