using Orbit.Core.Abstractions;

namespace Orbit.Core.Notifications.UpdateNotificationSettings;

public sealed class UpdateNotificationSettingsCommandHandler : IRequestHandler<UpdateNotificationSettingsCommand, NotificationSettings>
{
    private readonly INotificationSettingsRepository _notificationSettingsRepository;

    public UpdateNotificationSettingsCommandHandler(INotificationSettingsRepository notificationSettingsRepository)
    {
        _notificationSettingsRepository = notificationSettingsRepository;
    }

    public async Task<NotificationSettings> HandleAsync(UpdateNotificationSettingsCommand request, CancellationToken cancellationToken)
    {
        var settings = await _notificationSettingsRepository.GetAsync(request.UserId, cancellationToken);
        settings.Update(request.AllowNotifications, request.AllowPush, request.AllowEmail, request.AllowMobileBanner, request.ShowExceptionDetails);
        await _notificationSettingsRepository.UpsertAsync(settings, cancellationToken);
        return settings;
    }
}
