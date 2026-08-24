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
    }

    [Fact]
    public void Update_forces_the_three_child_switches_off_when_the_master_switch_is_off()
    {
        var settings = NotificationSettings.Default(Guid.NewGuid());

        settings.Update(allowNotifications: false, allowPush: true, allowEmail: true, allowMobileBanner: true, showExceptionDetails: true);

        Assert.False(settings.AllowNotifications);
        Assert.False(settings.AllowPush);
        Assert.False(settings.AllowEmail);
        Assert.False(settings.AllowMobileBanner);
        // Independent of the master switch - a separate concern (debug visibility, not notifications).
        Assert.True(settings.ShowExceptionDetails);
    }

    [Fact]
    public void Update_respects_individual_child_switches_when_the_master_switch_is_on()
    {
        var settings = NotificationSettings.Default(Guid.NewGuid());

        settings.Update(allowNotifications: true, allowPush: false, allowEmail: true, allowMobileBanner: false, showExceptionDetails: false);

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
        settings.Update(allowNotifications: true, allowPush: false, allowEmail: true, allowMobileBanner: true, showExceptionDetails: true);

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
}
