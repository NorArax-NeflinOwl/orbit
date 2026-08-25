using Orbit.Core.Users;

namespace Orbit.Core.Notifications;

/// <summary>Tells someone something has been shared with them - see <see cref="SharedItemNotifier"/>.</summary>
public interface ISharedItemNotifier
{
    Task NotifyAsync(
        Guid recipientUserId, Guid sharerUserId, SharedItemKind kind, string? itemTitle, CancellationToken cancellationToken);
}

/// <summary>
/// Tells someone that a note, task list, calendar event, warehouse, or position has been shared with
/// them. Every Share*CommandHandler goes through here, so being invited to something looks the same
/// whatever kind of thing it is.
///
/// The entry in the notification feed **is** the invitation, so it is always recorded (subject only to
/// the master AllowNotifications switch). Push and email are the extra on top, and only go out when the
/// recipient asked for them - see NotificationSettings.AllowShareNotifications, which starts off.
/// </summary>
public sealed class SharedItemNotifier : ISharedItemNotifier
{
    private readonly INotificationSettingsRepository _notificationSettingsRepository;
    private readonly NotificationRecorder _notificationRecorder;
    private readonly PushNotificationDispatcher _pushNotificationDispatcher;
    private readonly IUserRepository _userRepository;

    public SharedItemNotifier(
        INotificationSettingsRepository notificationSettingsRepository,
        NotificationRecorder notificationRecorder,
        PushNotificationDispatcher pushNotificationDispatcher,
        IUserRepository userRepository)
    {
        _notificationSettingsRepository = notificationSettingsRepository;
        _notificationRecorder = notificationRecorder;
        _pushNotificationDispatcher = pushNotificationDispatcher;
        _userRepository = userRepository;
    }

    /// <summary>
    /// Best-effort by design: the share itself has already happened by the time this runs, so a failure
    /// to announce it must not turn a successful share into a failed request. Callers treat it that way.
    /// </summary>
    public async Task NotifyAsync(
        Guid recipientUserId, Guid sharerUserId, SharedItemKind kind, string? itemTitle, CancellationToken cancellationToken)
    {
        var sharer = await _userRepository.GetByIdAsync(sharerUserId, cancellationToken);
        var sharerName = sharer?.DisplayName ?? "Someone";
        var settings = await _notificationSettingsRepository.GetAsync(recipientUserId, cancellationToken);

        var title = $"{sharerName} shared {Describe(kind)} with you";
        var body = string.IsNullOrWhiteSpace(itemTitle) ? title : itemTitle;

        var result = await _notificationRecorder.RecordAndFilterAsync(
            recipientUserId, settings.ChannelForShares(), NotificationEntryKind.SharedWithYou,
            title, body, UrlFor(kind, sharerUserId), cancellationToken);

        if (result.AllowedChannel.HasFlag(NotificationChannel.Push))
        {
            await _pushNotificationDispatcher.NotifyUserAsync(
                recipientUserId, new PushNotificationPayload(title, body, UrlFor(kind, sharerUserId)), cancellationToken);
        }
    }

    private static string Describe(SharedItemKind kind) => kind switch
    {
        SharedItemKind.Note => "a note",
        SharedItemKind.TaskList => "a task list",
        SharedItemKind.CalendarEvent => "an event",
        SharedItemKind.Warehouse => "a warehouse",
        _ => "their location"
    };

    /// <summary>
    /// Where the notification takes the recipient. A shared note, list, event or warehouse has to be
    /// accepted before it is theirs to open, and the Accept action lives in the conversation with
    /// whoever sent it - so pointing at the item itself would land on a "not found". A shared position
    /// needs no accepting and shows up on the map.
    /// </summary>
    private static string UrlFor(SharedItemKind kind, Guid sharerUserId)
        => kind == SharedItemKind.Location ? "/map" : $"/chat/{sharerUserId}";
}

/// <summary>What was shared - decides the wording and where the notification leads.</summary>
public enum SharedItemKind
{
    Note,
    TaskList,
    CalendarEvent,
    Warehouse,
    Location
}
