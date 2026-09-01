using System.Net;
using Orbit.Contracts.Notifications;
using Orbit.Mobile.Api;
using Orbit.Mobile.Screens.Notifications;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;
using Orbit.Mobile.Localization;

namespace Orbit.Mobile.Tests.Screens;

/// <summary>
/// The notification switches. These are account-wide rather than per-device, which is what makes the
/// save worth testing: the endpoint replaces the whole settings object, and this screen shows only the
/// part of it that means anything on a phone.
/// </summary>
public sealed class NotificationSettingsScreenTests
{
    [Fact]
    public async Task The_switches_open_showing_what_the_account_is_set_to()
    {
        var context = new SettingsContext();
        context.Server.Settings = context.Server.Settings with { AllowPush = false, AllowEmail = true };
        var screen = context.Open();

        await screen.LoadCommand.ExecuteAsync(null);

        Assert.False(screen.AllowPush);
        Assert.True(screen.AllowEmail);
    }

    [Fact]
    public async Task Saving_from_the_phone_leaves_what_it_does_not_show_alone()
    {
        // The trap this screen exists to avoid. The endpoint takes the whole object, and a phone that
        // sends defaults for what it does not show would silently undo a choice made in the browser - a
        // change nothing on the phone would ever reveal.
        //
        // ShowExceptionDetails is the one left: it decides how much of a failure Orbit.Web prints on the
        // page, and nothing on the phone reads it. The banner and retention settings used to be on this
        // list, which was the problem - the phone obeyed them and could not change them.
        var context = new SettingsContext();
        context.Server.Settings = context.Server.Settings with
        {
            BannerVisibleSeconds = 12, BannerMinimumGapSeconds = 30, ShowExceptionDetails = true, RetentionDays = 45
        };
        var screen = context.Open();
        await screen.LoadCommand.ExecuteAsync(null);

        screen.AllowEmail = true;
        await screen.SaveCommand.ExecuteAsync(null);

        Assert.True(context.Server.Settings.ShowExceptionDetails);
        // And what the phone does show goes back as it was read, rather than as a default.
        Assert.Equal(12, context.Server.Settings.BannerVisibleSeconds);
        Assert.Equal(30, context.Server.Settings.BannerMinimumGapSeconds);
        Assert.Equal(45, context.Server.Settings.RetentionDays);
        // And the one the reader actually changed did change.
        Assert.True(context.Server.Settings.AllowEmail);
    }

    /// <summary>
    /// The banner is the phone's own - ForegroundNotices shows it, and obeys all three of these - and
    /// until this screen offered them they could be changed only from a browser.
    /// </summary>
    [Fact]
    public async Task The_phones_own_banner_can_be_turned_off_from_the_phone()
    {
        var context = new SettingsContext();
        context.Server.Settings = context.Server.Settings with { AllowMobileBanner = true };
        var screen = context.Open();
        await screen.LoadCommand.ExecuteAsync(null);

        Assert.True(screen.AllowMobileBanner);

        screen.AllowMobileBanner = false;
        await screen.SaveCommand.ExecuteAsync(null);

        Assert.False(context.Server.Settings.AllowMobileBanner);
    }

    [Fact]
    public async Task How_long_a_banner_stays_and_how_soon_the_next_may_follow_are_saved()
    {
        var context = new SettingsContext();
        var screen = context.Open();
        await screen.LoadCommand.ExecuteAsync(null);

        screen.AllowMobileBanner = true;
        screen.BannerVisibleSeconds = 9;
        screen.BannerMinimumGapSeconds = 25;
        await screen.SaveCommand.ExecuteAsync(null);

        Assert.Equal(9, context.Server.Settings.BannerVisibleSeconds);
        Assert.Equal(25, context.Server.Settings.BannerMinimumGapSeconds);
    }

    [Fact]
    public async Task How_long_notifications_are_kept_is_saved()
    {
        var context = new SettingsContext();
        var screen = context.Open();
        await screen.LoadCommand.ExecuteAsync(null);

        screen.RetentionDays = 30;
        await screen.SaveCommand.ExecuteAsync(null);

        Assert.Equal(30, context.Server.Settings.RetentionDays);
    }

    /// <summary>
    /// The server clamps out-of-range numbers silently rather than refusing them, so a screen that sent
    /// one and went on showing it would be describing a setting that does not exist. Clamped here too,
    /// which is also what makes the two agree without a second round trip.
    /// </summary>
    [Theory]
    [InlineData(999, 30)]
    [InlineData(0, 1)]
    public async Task A_banner_length_out_of_range_is_saved_as_the_nearest_one_allowed(int asked, int expected)
    {
        var context = new SettingsContext();
        var screen = context.Open();
        await screen.LoadCommand.ExecuteAsync(null);

        screen.BannerVisibleSeconds = asked;
        await screen.SaveCommand.ExecuteAsync(null);

        Assert.Equal(expected, context.Server.Settings.BannerVisibleSeconds);
        Assert.Equal(expected, screen.BannerVisibleSeconds);
    }

    [Theory]
    [InlineData(365, 90)]
    [InlineData(0, 1)]
    public async Task A_retention_out_of_range_is_saved_as_the_nearest_one_allowed(int asked, int expected)
    {
        var context = new SettingsContext();
        var screen = context.Open();
        await screen.LoadCommand.ExecuteAsync(null);

        screen.RetentionDays = asked;
        await screen.SaveCommand.ExecuteAsync(null);

        Assert.Equal(expected, context.Server.Settings.RetentionDays);
        Assert.Equal(expected, screen.RetentionDays);
    }

    /// <summary>
    /// How long a banner stays means nothing when no banner is shown - the same rule Orbit.Web's Options
    /// applies to the same two fields.
    /// </summary>
    [Fact]
    public async Task The_banner_timings_are_offered_only_while_a_banner_will_be_shown()
    {
        var context = new SettingsContext();
        var screen = context.Open();
        await screen.LoadCommand.ExecuteAsync(null);

        screen.AllowNotifications = true;
        screen.AllowMobileBanner = false;
        Assert.False(screen.CanChooseBannerTiming);

        screen.AllowMobileBanner = true;
        Assert.True(screen.CanChooseBannerTiming);

        screen.AllowNotifications = false;
        Assert.False(screen.CanChooseBannerTiming);
    }

    /// <summary>
    /// The button, not just the method behind it. Every other test here runs SaveCommand directly, which
    /// skips CanExecute - so all of them passed while the Save button on the screen was disabled from the
    /// moment it opened: the load asks whether saving is possible while it is still marked busy, and
    /// clearing that afterwards never asked the command again. Settings could be read and never changed.
    /// </summary>
    [Fact]
    public async Task The_save_button_is_offered_once_the_settings_have_been_read()
    {
        var context = new SettingsContext();
        var screen = context.Open();

        // What the button sees, not what the predicate would say if asked: a button only re-asks when
        // it is told to, so the last thing it was told is the state it is left in.
        bool? whatTheButtonWasLastTold = null;
        screen.SaveCommand.CanExecuteChanged += (_, _) => whatTheButtonWasLastTold = screen.SaveCommand.CanExecute(null);

        await screen.LoadCommand.ExecuteAsync(null);

        Assert.True(whatTheButtonWasLastTold);
    }

    [Fact]
    public async Task Nothing_is_saved_before_anything_was_read()
    {
        // Saving what was never loaded would write guesses over real settings.
        var context = new SettingsContext();
        context.Server.IsUnreachable = true;
        var screen = context.Open();
        await screen.LoadCommand.ExecuteAsync(null);

        Assert.False(screen.CanSave);
    }

    [Fact]
    public async Task The_channel_switches_are_offered_only_while_notifications_are_on_at_all()
    {
        var context = new SettingsContext();
        var screen = context.Open();
        await screen.LoadCommand.ExecuteAsync(null);

        screen.AllowNotifications = false;

        // The master switch suppresses every channel below it, so offering them would be a lie.
        Assert.False(screen.CanChooseChannels);
    }

    [Fact]
    public async Task Being_out_of_reach_says_so()
    {
        var context = new SettingsContext();
        context.Server.IsUnreachable = true;
        var screen = context.Open();

        await screen.LoadCommand.ExecuteAsync(null);

        Assert.Contains("out of reach", screen.Message);
    }

    [Fact]
    public async Task A_refusal_is_not_reported_as_being_offline()
    {
        var context = new SettingsContext();
        context.Server.RefuseEverythingWith = HttpStatusCode.Unauthorized;
        var screen = context.Open();

        await screen.LoadCommand.ExecuteAsync(null);

        Assert.DoesNotContain("out of reach", screen.Message);
        Assert.Contains("signing in", screen.Message);
    }

    private sealed class SettingsContext
    {
        public FakeNotificationServer Server { get; } = new();

        public RecordingScreenNavigator Navigator { get; } = new();

        public NotificationSettingsViewModel Open()
            => new(
                new NotificationsClient(Server.ToHttpClient()),
                new Translations(new InMemoryLanguageStore()), Navigator);
    }
}
