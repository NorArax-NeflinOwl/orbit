using Orbit.Core.Abstractions;

namespace Orbit.Core.Notifications.GetNotificationSettings;

public sealed class GetNotificationSettingsQueryHandler : IRequestHandler<GetNotificationSettingsQuery, NotificationSettings>
{
    private readonly INotificationSettingsRepository _notificationSettingsRepository;

    public GetNotificationSettingsQueryHandler(INotificationSettingsRepository notificationSettingsRepository)
    {
        _notificationSettingsRepository = notificationSettingsRepository;
    }

    public Task<NotificationSettings> HandleAsync(GetNotificationSettingsQuery request, CancellationToken cancellationToken)
        => _notificationSettingsRepository.GetAsync(request.UserId, cancellationToken);
}
