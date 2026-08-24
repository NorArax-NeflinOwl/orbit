namespace Orbit.Core.Notifications;

/// <summary>
/// A user's account-wide notification preferences - one row per user, created lazily on first read
/// (see INotificationSettingsRepository.GetAsync) rather than at registration, so existing accounts
/// never need a migration touching every row. Distinct from NotificationChannel, which is a per-item
/// choice (this calendar event, that task) layered on top: these settings are the master switches that
/// override those per-item choices, not a replacement for them.
/// </summary>
public sealed class NotificationSettings
{
    public Guid UserId { get; private set; }

    /// <summary>Master switch: off suppresses every delivery channel below and stops new entries from being recorded in the notification feed at all.</summary>
    public bool AllowNotifications { get; private set; }

    public bool AllowPush { get; private set; }
    public bool AllowEmail { get; private set; }

    /// <summary>Whether a toast banner pops up for a new notification while the app is open - independent of whether the underlying push/email actually went out.</summary>
    public bool AllowMobileBanner { get; private set; }

    /// <summary>
    /// Whether a caught client-side exception's full details (with a copy-to-clipboard action) are
    /// shown to this user - always subject to the server's own environment gate too (see
    /// ClientFlagsDto.ExceptionDetailsAllowed), so this alone doesn't expose stack traces in Production
    /// no matter what a user sets it to.
    /// </summary>
    public bool ShowExceptionDetails { get; private set; }

    private NotificationSettings(
        Guid userId, bool allowNotifications, bool allowPush, bool allowEmail, bool allowMobileBanner, bool showExceptionDetails)
    {
        UserId = userId;
        AllowNotifications = allowNotifications;
        AllowPush = allowPush;
        AllowEmail = allowEmail;
        AllowMobileBanner = allowMobileBanner;
        ShowExceptionDetails = showExceptionDetails;
    }

    /// <summary>Every switch defaults to on - this is what a user who has never touched the settings page gets.</summary>
    public static NotificationSettings Default(Guid userId)
        => new(userId, allowNotifications: true, allowPush: true, allowEmail: true, allowMobileBanner: true, showExceptionDetails: true);

    public static NotificationSettings FromPersistence(
        Guid userId, bool allowNotifications, bool allowPush, bool allowEmail, bool allowMobileBanner, bool showExceptionDetails)
        => new(userId, allowNotifications, allowPush, allowEmail, allowMobileBanner, showExceptionDetails);

    /// <summary>
    /// The three delivery/display switches are stored as the caller set them, but Update forces them off
    /// whenever the master switch is off - matches the Blazor client's own greyed-out-when-master-off
    /// checkbox group, and means every other reader of this type (the background services filtering a
    /// per-item NotificationChannel, the banner) only has to check its own flag, never AllowNotifications
    /// as well.
    /// </summary>
    public void Update(bool allowNotifications, bool allowPush, bool allowEmail, bool allowMobileBanner, bool showExceptionDetails)
    {
        AllowNotifications = allowNotifications;
        AllowPush = allowNotifications && allowPush;
        AllowEmail = allowNotifications && allowEmail;
        AllowMobileBanner = allowNotifications && allowMobileBanner;
        ShowExceptionDetails = showExceptionDetails;
    }

    /// <summary>
    /// Strips any channel this user has globally disabled out of a per-item NotificationChannel before a
    /// background service acts on it - the global switch overrides the per-item choice, not the other
    /// way around. Callers still check the individual flags with HasFlag afterward exactly as before.
    /// </summary>
    public NotificationChannel FilterChannel(NotificationChannel requested)
    {
        var allowed = NotificationChannel.None;
        if (AllowPush && requested.HasFlag(NotificationChannel.Push))
        {
            allowed |= NotificationChannel.Push;
        }
        if (AllowEmail && requested.HasFlag(NotificationChannel.Email))
        {
            allowed |= NotificationChannel.Email;
        }

        return allowed;
    }
}
