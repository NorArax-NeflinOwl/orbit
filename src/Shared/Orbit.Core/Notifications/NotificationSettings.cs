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
    /// The three delivery/display switches are stored exactly as the caller set them, independent of the
    /// master switch - turning AllowNotifications off must not erase what the user had previously chosen
    /// for push/email/banner, or re-enabling it would silently lose those preferences. Readers that need
    /// the *effective* value (FilterChannel, NotificationChannelOption.IsDisabledBy) check AllowNotifications
    /// themselves rather than relying on it having been baked into these three flags at save time.
    /// </summary>
    public void Update(bool allowNotifications, bool allowPush, bool allowEmail, bool allowMobileBanner, bool showExceptionDetails)
    {
        AllowNotifications = allowNotifications;
        AllowPush = allowPush;
        AllowEmail = allowEmail;
        AllowMobileBanner = allowMobileBanner;
        ShowExceptionDetails = showExceptionDetails;
    }

    /// <summary>
    /// Strips any channel this user has globally disabled out of a per-item NotificationChannel before a
    /// background service acts on it - the global switch overrides the per-item choice, not the other
    /// way around. Callers still check the individual flags with HasFlag afterward exactly as before.
    /// </summary>
    public NotificationChannel FilterChannel(NotificationChannel requested)
    {
        if (!AllowNotifications)
        {
            return NotificationChannel.None;
        }

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
