using Orbit.Core.Calendar;
using Orbit.Core.Calendar.Reminders;
using Orbit.Core.Notifications;
using Xunit;

namespace Orbit.Api.Tests.Calendar.Reminders;

public sealed class EventReminderPushContentTests
{
    private static readonly CalendarEventDetails DefaultDetails = new(
        "Stand-up", null, null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(30), false, null, [], [10], ReminderNotificationChannel: NotificationChannel.Push);

    [Fact]
    public void Build_includes_the_events_title_in_the_body()
    {
        var payload = EventReminderPushContent.Build(DefaultDetails, Guid.NewGuid(), 10);

        Assert.Contains("Stand-up", payload.Body);
    }

    [Fact]
    public void Build_points_the_url_at_the_calendar_event()
    {
        var eventId = Guid.NewGuid();

        var payload = EventReminderPushContent.Build(DefaultDetails, eventId, 10);

        Assert.Equal($"/calendar/{eventId}", payload.Url);
    }

    [Theory]
    [InlineData(0, "now")]
    [InlineData(10, "in 10 min")]
    [InlineData(60, "in 1 hr")]
    public void Build_formats_the_lead_time_in_the_body(int minutesBeforeStart, string expectedLeadTimeText)
    {
        var payload = EventReminderPushContent.Build(DefaultDetails, Guid.NewGuid(), minutesBeforeStart);

        Assert.Contains(expectedLeadTimeText, payload.Body);
    }
}
