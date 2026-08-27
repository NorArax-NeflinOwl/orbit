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
    public async Task Saving_from_the_phone_leaves_the_browser_only_settings_alone()
    {
        // The trap this screen exists to avoid. The endpoint takes the whole object, and a phone that
        // sends defaults for the switches it does not show would silently undo somebody's tuning in the
        // browser - a change nothing on the phone would ever reveal.
        var context = new SettingsContext();
        context.Server.Settings = context.Server.Settings with
        {
            BannerVisibleSeconds = 12, BannerMinimumGapSeconds = 30, ShowExceptionDetails = true, RetentionDays = 45
        };
        var screen = context.Open();
        await screen.LoadCommand.ExecuteAsync(null);

        screen.AllowEmail = true;
        await screen.SaveCommand.ExecuteAsync(null);

        Assert.Equal(12, context.Server.Settings.BannerVisibleSeconds);
        Assert.Equal(30, context.Server.Settings.BannerMinimumGapSeconds);
        Assert.True(context.Server.Settings.ShowExceptionDetails);
        Assert.Equal(45, context.Server.Settings.RetentionDays);
        // And the one the reader actually changed did change.
        Assert.True(context.Server.Settings.AllowEmail);
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
