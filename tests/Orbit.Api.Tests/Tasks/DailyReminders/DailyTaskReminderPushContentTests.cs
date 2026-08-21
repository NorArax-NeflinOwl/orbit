using Orbit.Core.Notifications;
using Orbit.Core.Tasks.DailyReminders;
using Xunit;

namespace Orbit.Api.Tests.Tasks.DailyReminders;

public sealed class DailyTaskReminderPushContentTests
{
    [Fact]
    public void Build_includes_the_items_description_in_the_body_and_points_the_url_at_the_task_list()
    {
        var taskListId = Guid.NewGuid();
        var reminder = new DueDailyTaskReminder(
            Guid.NewGuid(), taskListId, Guid.NewGuid(), "Groceries", "Buy milk", null,
            NotificationChannel.Push, DateOnly.FromDateTime(DateTime.Today));

        var payload = DailyTaskReminderPushContent.Build(reminder);

        Assert.Contains("Buy milk", payload.Body);
        Assert.Contains("Groceries", payload.Body);
        Assert.Equal($"/tasks/{taskListId}", payload.Url);
    }
}
