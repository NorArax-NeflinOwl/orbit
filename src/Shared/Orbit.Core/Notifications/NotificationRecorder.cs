namespace Orbit.Core.Notifications;

/// <summary>
/// The single place a notification trigger (the four reminder background services, SendMessageCommandHandler)
/// goes through before dispatching push/email and/or recording a feed entry - keeps the "check the
/// user's global settings" logic in one spot instead of duplicating it at every call site. See
/// NotificationRecordResult for what each half of the result means.
/// </summary>
public sealed class NotificationRecorder
{
    private readonly INotificationSettingsRepository _notificationSettingsRepository;
    private readonly INotificationEntryRepository _notificationEntryRepository;

    public NotificationRecorder(
        INotificationSettingsRepository notificationSettingsRepository, INotificationEntryRepository notificationEntryRepository)
    {
        _notificationSettingsRepository = notificationSettingsRepository;
        _notificationEntryRepository = notificationEntryRepository;
    }

    public async Task<NotificationRecordResult> RecordAndFilterAsync(
        Guid userId, NotificationChannel requestedChannel, NotificationEntryKind kind, string title, string body, string? url,
        CancellationToken cancellationToken)
    {
        var settings = await _notificationSettingsRepository.GetAsync(userId, cancellationToken);
        if (settings.AllowNotifications)
        {
            await _notificationEntryRepository.AddAsync(NotificationEntry.Create(userId, kind, title, body, url), cancellationToken);
        }

        return new NotificationRecordResult(settings.FilterChannel(requestedChannel), EntryRecorded: settings.AllowNotifications);
    }
}
