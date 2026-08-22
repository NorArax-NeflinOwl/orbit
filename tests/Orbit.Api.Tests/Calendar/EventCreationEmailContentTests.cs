using Orbit.Core.Calendar;
using Orbit.Core.Notifications;
using Xunit;

namespace Orbit.Api.Tests.Calendar;

public sealed class EventCreationEmailContentTests
{
    private static readonly CalendarEventDetails DefaultDetails = new(
        "Stand-up", null, null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(30), false, null, [], [],
        CreationNotificationChannel: NotificationChannel.Email, ReminderNotificationChannel: NotificationChannel.None);

    [Fact]
    public void Build_includes_the_events_title_in_the_subject()
    {
        var (subject, _) = EventCreationEmailContent.Build(DefaultDetails);

        Assert.Contains("Stand-up", subject);
    }

    [Fact]
    public void Build_includes_the_description_when_present()
    {
        var details = DefaultDetails with { Description = "Daily sync with the team" };

        var (_, body) = EventCreationEmailContent.Build(details);

        Assert.Contains("Daily sync with the team", body);
    }

    [Fact]
    public void Build_omits_a_description_line_when_none_is_set()
    {
        var (_, body) = EventCreationEmailContent.Build(DefaultDetails);

        Assert.DoesNotContain("Description:", body);
    }

    [Fact]
    public void Build_marks_all_day_events_instead_of_showing_a_time_of_day()
    {
        var details = DefaultDetails with { IsAllDay = true };

        var (_, body) = EventCreationEmailContent.Build(details);

        Assert.Contains("all day", body);
    }
}
