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
    /// Whether being shared something - a note, task list, event, inventory, or someone's position -
    /// also pushes or emails, on top of the entry that always appears in the notification feed.
    /// Off by default, unlike every other switch here: an invitation is worth seeing when you next look,
    /// but interrupting someone for one is a choice they should make rather than have made for them.
    /// </summary>
    public bool AllowShareNotifications { get; private set; }

    /// <summary>
    /// Whether a caught client-side exception's full details (with a copy-to-clipboard action) are
    /// shown to this user - always subject to the server's own environment gate too (see
    /// ClientFlagsDto.ExceptionDetailsAllowed), so this alone doesn't expose stack traces in Production
    /// no matter what a user sets it to.
    /// </summary>
    public bool ShowExceptionDetails { get; private set; }

    /// <summary>See <see cref="Notifications.BannerTiming"/> - stored per user so the Options page can tune it.</summary>
    public BannerTiming BannerTiming { get; private set; }

    /// <summary>
    /// How many days a notification is kept before it is deleted for good. Clearing the panel only
    /// hides an entry (see NotificationEntry.Dismiss); this is what actually removes it, dismissed or
    /// not, so the feed doesn't grow without limit and an old notification doesn't outlive its point.
    /// </summary>
    public int RetentionDays { get; private set; }

    /// <summary>Three days: long enough to catch up after a weekend, short enough that the list stays a list of what is going on.</summary>
    public const int DefaultRetentionDays = 3;

    public const int MinimumRetentionDays = 1;
    public const int MaximumRetentionDays = 90;

    /// <summary>
    /// Clamped rather than rejected: this arrives from a settings form, and a nonsensical number there
    /// should leave the reader with a working setting rather than a failed save.
    /// </summary>
    private static int ClampRetentionDays(int retentionDays)
        => Math.Clamp(retentionDays, MinimumRetentionDays, MaximumRetentionDays);

    private NotificationSettings(
        Guid userId, bool allowNotifications, bool allowPush, bool allowEmail, bool allowMobileBanner, bool showExceptionDetails,
        bool allowShareNotifications, BannerTiming bannerTiming, int retentionDays)
    {
        UserId = userId;
        AllowNotifications = allowNotifications;
        AllowPush = allowPush;
        AllowEmail = allowEmail;
        AllowMobileBanner = allowMobileBanner;
        ShowExceptionDetails = showExceptionDetails;
        AllowShareNotifications = allowShareNotifications;
        BannerTiming = bannerTiming;
        RetentionDays = ClampRetentionDays(retentionDays);
    }

    /// <summary>
    /// What a user who has never touched the settings page gets: every switch on, except being pushed
    /// or emailed about a share - see AllowShareNotifications for why that one starts off.
    /// </summary>
    public static NotificationSettings Default(Guid userId)
        => new(
            userId, allowNotifications: true, allowPush: true, allowEmail: true, allowMobileBanner: true, showExceptionDetails: true,
            allowShareNotifications: false, BannerTiming.Default, DefaultRetentionDays);

    public static NotificationSettings FromPersistence(
        Guid userId, bool allowNotifications, bool allowPush, bool allowEmail, bool allowMobileBanner, bool showExceptionDetails,
        bool allowShareNotifications, BannerTiming bannerTiming, int retentionDays)
        => new(userId, allowNotifications, allowPush, allowEmail, allowMobileBanner, showExceptionDetails,
            allowShareNotifications, bannerTiming, retentionDays);

    /// <summary>
    /// The three delivery/display switches are stored exactly as the caller set them, independent of the
    /// master switch - turning AllowNotifications off must not erase what the user had previously chosen
    /// for push/email/banner, or re-enabling it would silently lose those preferences. Readers that need
    /// the *effective* value (FilterChannel, NotificationChannelOption.IsDisabledBy) check AllowNotifications
    /// themselves rather than relying on it having been baked into these three flags at save time.
    /// </summary>
    public void Update(
        bool allowNotifications, bool allowPush, bool allowEmail, bool allowMobileBanner, bool showExceptionDetails,
        bool allowShareNotifications, BannerTiming bannerTiming, int retentionDays)
    {
        AllowNotifications = allowNotifications;
        AllowPush = allowPush;
        AllowEmail = allowEmail;
        AllowMobileBanner = allowMobileBanner;
        ShowExceptionDetails = showExceptionDetails;
        AllowShareNotifications = allowShareNotifications;
        BannerTiming = bannerTiming;
        RetentionDays = ClampRetentionDays(retentionDays);
    }

    /// <summary>
    /// Which channels a share should go out on for this user: none unless they asked to be told, and
    /// then only the ones they have on anyway (FilterChannel applies the master and per-channel
    /// switches). The feed entry is recorded either way - see NotificationEntryKind.SharedWithYou.
    /// </summary>
    public NotificationChannel ChannelForShares()
        => AllowShareNotifications ? FilterChannel(NotificationChannel.Both) : NotificationChannel.None;

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
