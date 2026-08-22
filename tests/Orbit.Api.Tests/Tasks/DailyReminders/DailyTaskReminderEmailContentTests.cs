using Orbit.Core.Notifications;
using Orbit.Core.Tasks.DailyReminders;
using Xunit;

namespace Orbit.Api.Tests.Tasks.DailyReminders;

public sealed class DailyTaskReminderEmailContentTests
{
    [Fact]
    public void Build_includes_the_items_description_in_the_subject_and_body()
    {
        var reminder = new DueDailyTaskReminder(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Groceries", "Buy milk", DateTimeOffset.UtcNow.AddDays(1),
            NotificationChannel.Email, DateOnly.FromDateTime(DateTime.Today));

        var (subject, body) = DailyTaskReminderEmailContent.Build(reminder);

        Assert.Contains("Buy milk", subject);
        Assert.Contains("Buy milk", body);
        Assert.Contains("Groceries", body);
    }

    [Fact]
    public void Build_omits_a_due_date_line_when_the_item_has_no_due_date()
    {
        var reminder = new DueDailyTaskReminder(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Groceries", "Buy milk", null,
            NotificationChannel.Email, DateOnly.FromDateTime(DateTime.Today));

        var (_, body) = DailyTaskReminderEmailContent.Build(reminder);

        Assert.DoesNotContain("Due:", body);
    }
}
