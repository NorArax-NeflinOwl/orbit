using Orbit.Core.Notifications;
using Xunit;

namespace Orbit.Api.Tests.Notifications;

public sealed class NotificationSettingsTests
{
    [Fact]
    public void Default_turns_every_switch_on()
    {
        var settings = NotificationSettings.Default(Guid.NewGuid());

        Assert.True(settings.AllowNotifications);
        Assert.True(settings.AllowPush);
        Assert.True(settings.AllowEmail);
        Assert.True(settings.AllowMobileBanner);
        Assert.True(settings.ShowExceptionDetails);
        Assert.Equal(BannerTiming.Default, settings.BannerTiming);
    }

    [Fact]
    public void BannerTiming_clamps_out_of_range_values_instead_of_rejecting_them()
    {
        var timing = new BannerTiming(visibleSeconds: 0, minimumGapSeconds: 9999);

        Assert.Equal(BannerTiming.MinimumSeconds, timing.VisibleSeconds);
        Assert.Equal(BannerTiming.MaximumGapSeconds, timing.MinimumGapSeconds);
    }

    [Fact]
    public void Update_stores_the_new_banner_timing()
    {
        var settings = NotificationSettings.Default(Guid.NewGuid());

        settings.Update(
            allowNotifications: true, allowPush: true, allowEmail: true, allowMobileBanner: true, showExceptionDetails: true,
            allowShareNotifications: false, new BannerTiming(visibleSeconds: 12, minimumGapSeconds: 45),
            NotificationSettings.DefaultRetentionDays);

        Assert.Equal(12, settings.BannerTiming.VisibleSeconds);
        Assert.Equal(45, settings.BannerTiming.MinimumGapSeconds);
    }

    [Fact]
    public void Update_preserves_the_three_child_switches_when_the_master_switch_is_off()
    {
        var settings = NotificationSettings.Default(Guid.NewGuid());

        settings.Update(allowNotifications: false, allowPush: true, allowEmail: true, allowMobileBanner: true, showExceptionDetails: true, allowShareNotifications: false, BannerTiming.Default, NotificationSettings.DefaultRetentionDays);

        Assert.False(settings.AllowNotifications);
        // Turning the master off must not erase what the user had chosen for each channel - otherwise
        // re-enabling it would silently lose those preferences instead of restoring them.
        Assert.True(settings.AllowPush);
        Assert.True(settings.AllowEmail);
        Assert.True(settings.AllowMobileBanner);
        Assert.True(settings.ShowExceptionDetails);
    }

    [Fact]
    public void Update_respects_individual_child_switches_when_the_master_switch_is_on()
    {
        var settings = NotificationSettings.Default(Guid.NewGuid());

        settings.Update(allowNotifications: true, allowPush: false, allowEmail: true, allowMobileBanner: false, showExceptionDetails: false, allowShareNotifications: false, BannerTiming.Default, NotificationSettings.DefaultRetentionDays);

        Assert.True(settings.AllowNotifications);
        Assert.False(settings.AllowPush);
        Assert.True(settings.AllowEmail);
        Assert.False(settings.AllowMobileBanner);
        Assert.False(settings.ShowExceptionDetails);
    }

    [Fact]
    public void FilterChannel_strips_a_globally_disabled_channel_out_of_the_requested_channel()
    {
        var settings = NotificationSettings.Default(Guid.NewGuid());
        settings.Update(allowNotifications: true, allowPush: false, allowEmail: true, allowMobileBanner: true, showExceptionDetails: true, allowShareNotifications: false, BannerTiming.Default, NotificationSettings.DefaultRetentionDays);

        var filtered = settings.FilterChannel(NotificationChannel.Both);

        Assert.Equal(NotificationChannel.Email, filtered);
    }

    [Fact]
    public void FilterChannel_never_adds_a_channel_the_caller_did_not_request()
    {
        var settings = NotificationSettings.Default(Guid.NewGuid());

        var filtered = settings.FilterChannel(NotificationChannel.Push);

        Assert.Equal(NotificationChannel.Push, filtered);
    }

    [Fact]
    public void FilterChannel_strips_everything_when_the_master_switch_is_off_even_if_child_switches_are_on()
    {
        var settings = NotificationSettings.Default(Guid.NewGuid());
        settings.Update(allowNotifications: false, allowPush: true, allowEmail: true, allowMobileBanner: true, showExceptionDetails: true, allowShareNotifications: false, BannerTiming.Default, NotificationSettings.DefaultRetentionDays);

        var filtered = settings.FilterChannel(NotificationChannel.Both);

        Assert.Equal(NotificationChannel.None, filtered);
    }
}
