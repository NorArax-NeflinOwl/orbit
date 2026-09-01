using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Orbit.Contracts.Notifications;
using Orbit.Mobile.Api;
using Orbit.Mobile.Notifications;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Notifications;

/// <summary>
/// What a push does when it arrives while somebody is looking at the app.
///
/// Nothing, until now: Orbit's messages carry a notification block, which Android shows itself - but
/// only in the background. In the foreground the system hands the message to the app and shows nothing,
/// so a notification arriving with Orbit open was silently dropped. The feed had it on the next read,
/// and the moment it happened passed unremarked.
/// </summary>
public sealed class ForegroundNoticeTests
{
    private static readonly ForegroundNotice Arrived = new("New message", "Ala wrote to you", "/chat/x");

    [Fact]
    public async Task A_notification_arriving_with_the_app_open_is_shown()
    {
        using var context = new BannerContext();

        Assert.True(await context.Banners.ShowAsync(Arrived));
        Assert.Equal(Arrived, context.Banners.Showing);
    }

    /// <summary>
    /// The setting existed for exactly this and had no reader on this phone - see
    /// NotificationSettingsDto.AllowMobileBanner.
    /// </summary>
    [Fact]
    public async Task An_account_that_turned_banners_off_is_shown_nothing()
    {
        using var context = new BannerContext();
        context.Server.Settings = context.Server.Settings with { AllowMobileBanner = false };

        Assert.False(await context.Banners.ShowAsync(Arrived));
        Assert.Null(context.Banners.Showing);
    }

    [Fact]
    public async Task An_account_with_notifications_off_is_shown_nothing_either()
    {
        using var context = new BannerContext();
        context.Server.Settings = context.Server.Settings with { AllowNotifications = false };

        Assert.False(await context.Banners.ShowAsync(Arrived));
    }

    /// <summary>Three messages in a row is a conversation, not three interruptions.</summary>
    [Fact]
    public async Task A_second_one_too_soon_after_the_first_waits()
    {
        using var context = new BannerContext();
        await context.Banners.ShowAsync(Arrived);

        Assert.False(await context.Banners.ShowAsync(Arrived));
    }

    [Fact]
    public async Task Once_the_gap_has_passed_the_next_one_is_shown()
    {
        using var context = new BannerContext();
        await context.Banners.ShowAsync(Arrived);

        context.Clock.Advance(TimeSpan.FromSeconds(context.Server.Settings.BannerMinimumGapSeconds + 1));

        Assert.True(await context.Banners.ShowAsync(Arrived));
    }

    /// <summary>
    /// Unknown settings are not permission. This is called from a push callback with no connection
    /// guaranteed, and the tray already showed the message to anybody who was not looking.
    /// </summary>
    [Fact]
    public async Task Settings_that_could_not_be_read_show_nothing_rather_than_guessing()
    {
        using var context = new BannerContext();
        context.Server.IsUnreachable = true;

        Assert.False(await context.Banners.ShowAsync(Arrived));
    }

    [Fact]
    public async Task Signing_out_forgets_what_the_last_account_chose()
    {
        using var context = new BannerContext();
        await context.Banners.ShowAsync(Arrived);

        context.Banners.Forget();

        // The gap is forgotten with the settings, so the next account is not made to wait out the last
        // one's banner - and nothing is left on screen from an account that has gone.
        Assert.Null(context.Banners.Showing);
        Assert.True(await context.Banners.ShowAsync(Arrived));
    }

    private sealed class BannerContext : IDisposable
    {
        public BannerContext()
            => Banners = new ForegroundNotices(
                new NotificationsClient(Server.ToHttpClient()), Clock,
                NullLogger<ForegroundNotices>.Instance);

        public FakeNotificationServer Server { get; } = new();

        public FakeTimeProvider Clock { get; } = new(DateTimeOffset.Parse("2026-09-01T10:00:00Z"));

        public ForegroundNotices Banners { get; }

        public void Dispose() => Server.Dispose();
    }
}
