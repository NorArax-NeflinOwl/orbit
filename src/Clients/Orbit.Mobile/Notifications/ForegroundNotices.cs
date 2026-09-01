using Microsoft.Extensions.Logging;
using Orbit.Contracts.Notifications;
using Orbit.Mobile.Api;

namespace Orbit.Mobile.Notifications;

/// <summary>One notification, as a banner shows it - what it says and where tapping it goes.</summary>
public sealed record ForegroundNotice(string Title, string Body, string? Url);

/// <summary>
/// What a push does when it arrives while somebody is looking at the app.
///
/// Nothing, until now. Orbit's messages carry a notification block, which Android shows itself - but
/// only while the app is in the background. In the foreground the system hands the message to the app
/// and shows nothing, so a notification arriving while the app was open was silently dropped: the feed
/// had it on the next read, and the moment it happened passed unremarked.
///
/// A banner rather than a second tray notification. The tray already covers the case where nobody is
/// looking; posting there as well would put a heads-up over the very screen the message is about, and
/// the browser answers this the same way (see Orbit.Web's MainLayout).
///
/// Honours the settings that already existed for it and had no reader on this phone:
/// <see cref="NotificationSettingsDto.AllowMobileBanner"/>, and the two that pace it.
/// </summary>
public sealed class ForegroundNotices
{
    private readonly NotificationsClient _notifications;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ForegroundNotices> _logger;

    private NotificationSettingsDto? _settings;
    private DateTimeOffset? _lastShownAtUtc;

    public ForegroundNotices(
        NotificationsClient notifications, TimeProvider timeProvider, ILogger<ForegroundNotices> logger)
    {
        _notifications = notifications;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>Raised with what to show, and with null when it is time to take it away again.</summary>
    public event Action<ForegroundNotice?>? Changed;

    /// <summary>What is on screen right now, or null - read by the bar that draws it.</summary>
    public ForegroundNotice? Showing { get; private set; }

    /// <summary>How long it stays, once it is up. From the account's own settings.</summary>
    public TimeSpan VisibleFor => TimeSpan.FromSeconds(_settings?.BannerVisibleSeconds ?? 5);

    /// <summary>
    /// Shows one, unless the account has banners off or one went up too recently. Returns whether it
    /// did, which is what a test asks and what saves the caller starting a timer for nothing.
    ///
    /// Never throws: this is called from a push callback, and a banner is not worth taking a delivery
    /// down for.
    /// </summary>
    public async Task<bool> ShowAsync(ForegroundNotice notice, CancellationToken cancellationToken = default)
    {
        try
        {
            _settings ??= await _notifications.GetSettingsAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            // Unknown settings are not permission. The tray already showed this to anybody who was not
            // looking, so the cost of staying quiet here is one missed banner.
            _logger.LogDebug(exception, "Could not read the notification settings; not showing a banner");
            return false;
        }

        if (_settings is not { AllowNotifications: true, AllowMobileBanner: true })
        {
            return false;
        }

        var now = _timeProvider.GetUtcNow();
        if (_lastShownAtUtc is { } last
            && now - last < TimeSpan.FromSeconds(_settings.BannerMinimumGapSeconds))
        {
            // Three messages in a row is a conversation, not three interruptions.
            return false;
        }

        _lastShownAtUtc = now;
        Showing = notice;
        Changed?.Invoke(notice);
        return true;
    }

    /// <summary>Takes it away - when its time is up, and when somebody taps or dismisses it.</summary>
    public void Hide()
    {
        if (Showing is null)
        {
            return;
        }

        Showing = null;
        Changed?.Invoke(null);
    }

    /// <summary>
    /// Forgets what the account chose, so the next banner asks again. Called on sign-out: the settings
    /// belong to the account, not to the app.
    /// </summary>
    public void Forget()
    {
        _settings = null;
        _lastShownAtUtc = null;
        Hide();
    }
}
