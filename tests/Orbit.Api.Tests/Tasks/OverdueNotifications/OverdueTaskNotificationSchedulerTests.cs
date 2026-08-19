using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Tasks.OverdueNotifications;
using Xunit;

namespace Orbit.Api.Tests.Tasks.OverdueNotifications;

public sealed class OverdueTaskNotificationSchedulerTests
{
    [Fact]
    public async Task FindNewlyOverdueAsync_returns_an_item_whose_due_date_has_passed()
    {
        var now = DateTimeOffset.UtcNow;
        var overdueItem = CreateCandidate(dueDateUtc: now.AddMinutes(-5));
        var repository = new InMemoryOverdueTaskNotificationRepository([overdueItem]);
        var scheduler = new OverdueTaskNotificationScheduler(repository);

        var results = await scheduler.FindNewlyOverdueAsync(now, CancellationToken.None);

        Assert.Equal(overdueItem, Assert.Single(results));
    }

    [Fact]
    public async Task FindNewlyOverdueAsync_does_not_return_an_item_that_is_not_due_yet()
    {
        var now = DateTimeOffset.UtcNow;
        var notYetDueItem = CreateCandidate(dueDateUtc: now.AddMinutes(5));
        var repository = new InMemoryOverdueTaskNotificationRepository([notYetDueItem]);
        var scheduler = new OverdueTaskNotificationScheduler(repository);

        var results = await scheduler.FindNewlyOverdueAsync(now, CancellationToken.None);

        Assert.Empty(results);
    }

    [Fact]
    public async Task FindNewlyOverdueAsync_does_not_return_an_item_that_was_already_notified_about()
    {
        var now = DateTimeOffset.UtcNow;
        var overdueItem = CreateCandidate(dueDateUtc: now.AddMinutes(-5));
        var repository = new InMemoryOverdueTaskNotificationRepository([overdueItem]);
        await repository.TryClaimAsync(overdueItem.TaskItemId, now, CancellationToken.None);
        var scheduler = new OverdueTaskNotificationScheduler(repository);

        var results = await scheduler.FindNewlyOverdueAsync(now, CancellationToken.None);

        Assert.Empty(results);
    }

    [Fact]
    public async Task FindNewlyOverdueAsync_caps_the_number_of_items_returned_at_max_results()
    {
        var now = DateTimeOffset.UtcNow;
        var overdueItems = Enumerable.Range(0, 3).Select(_ => CreateCandidate(now.AddMinutes(-5))).ToList();
        var repository = new InMemoryOverdueTaskNotificationRepository(overdueItems);
        var scheduler = new OverdueTaskNotificationScheduler(repository);

        var results = await scheduler.FindNewlyOverdueAsync(now, CancellationToken.None, maxResults: 2);

        Assert.Equal(2, results.Count);
    }

    private static OverdueTaskItem CreateCandidate(DateTimeOffset dueDateUtc)
        => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Groceries", "Buy milk", dueDateUtc);
}
