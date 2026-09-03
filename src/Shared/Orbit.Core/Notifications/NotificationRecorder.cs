using Orbit.Core.LiveUpdates;

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
    private readonly ILiveUpdatePublisher _liveUpdatePublisher;

    public NotificationRecorder(
        INotificationSettingsRepository notificationSettingsRepository,
        INotificationEntryRepository notificationEntryRepository,
        ILiveUpdatePublisher liveUpdatePublisher)
    {
        _notificationSettingsRepository = notificationSettingsRepository;
        _notificationEntryRepository = notificationEntryRepository;
        _liveUpdatePublisher = liveUpdatePublisher;
    }

    public async Task<NotificationRecordResult> RecordAndFilterAsync(
        Guid userId, NotificationChannel requestedChannel, NotificationEntryKind kind,
        PushNotificationPayload says, CancellationToken cancellationToken)
    {
        var settings = await _notificationSettingsRepository.GetAsync(userId, cancellationToken);
        if (settings.AllowNotifications)
        {
            await _notificationEntryRepository.AddAsync(NotificationEntry.Create(userId, kind, says), cancellationToken);

            // Announced from here rather than from each trigger, because every one of them - the four
            // reminder services and SendMessageCommandHandler - already comes through this method. One
            // call covers all of them, and a trigger added later gets it without having to remember.
            await _liveUpdatePublisher.NotificationsChangedAsync(userId, cancellationToken);
        }

        return new NotificationRecordResult(settings.FilterChannel(requestedChannel), EntryRecorded: settings.AllowNotifications);
    }
}
