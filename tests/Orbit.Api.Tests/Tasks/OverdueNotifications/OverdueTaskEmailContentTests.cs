using Orbit.Core.Notifications;
using Orbit.Core.Tasks.OverdueNotifications;
using Xunit;

namespace Orbit.Api.Tests.Tasks.OverdueNotifications;

public sealed class OverdueTaskEmailContentTests
{
    [Fact]
    public void Build_includes_the_items_description_in_the_subject_and_body()
    {
        var overdueTaskItem = new OverdueTaskItem(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Groceries", "Buy milk", DateTimeOffset.UtcNow.AddMinutes(-5),
            NotificationChannel.Email);

        var (subject, body) = OverdueTaskEmailContent.Build(overdueTaskItem);

        Assert.Contains("Buy milk", subject);
        Assert.Contains("Buy milk", body);
        Assert.Contains("Groceries", body);
    }
}
