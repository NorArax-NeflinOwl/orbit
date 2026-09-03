using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Inventories.ExpiryReminders;
using Orbit.Core.Notifications;
using Xunit;

namespace Orbit.Api.Tests.Inventories.ExpiryReminders;

public sealed class InventoryExpiryReminderSchedulerTests
{
    [Fact]
    public async Task FindDueRemindersAsync_returns_an_item_expiring_within_the_lead_time()
    {
        var now = DateTimeOffset.UtcNow;
        var reminder = CreateCandidate(expiryDate: now.AddDays(2));
        var repository = new InMemoryInventoryExpiryNotificationRepository([reminder]);
        var scheduler = new InventoryExpiryReminderScheduler(repository);

        var results = await scheduler.FindDueRemindersAsync(now, CancellationToken.None);

        Assert.Equal(reminder, Assert.Single(results));
    }

    [Fact]
    public async Task FindDueRemindersAsync_does_not_return_an_item_expiring_beyond_the_lead_time()
    {
        var now = DateTimeOffset.UtcNow;
        var farFromExpiry = CreateCandidate(expiryDate: now.AddDays(10));
        var repository = new InMemoryInventoryExpiryNotificationRepository([farFromExpiry]);
        var scheduler = new InventoryExpiryReminderScheduler(repository);

        var results = await scheduler.FindDueRemindersAsync(now, CancellationToken.None);

        Assert.Empty(results);
    }

    [Fact]
    public async Task FindDueRemindersAsync_does_not_return_an_item_already_warned_about_for_that_expiry_date()
    {
        var now = DateTimeOffset.UtcNow;
        var reminder = CreateCandidate(expiryDate: now.AddDays(1));
        var repository = new InMemoryInventoryExpiryNotificationRepository([reminder]);
        await repository.TryClaimAsync(reminder.InventoryItemId, reminder.ExpiryDate, now, CancellationToken.None);
        var scheduler = new InventoryExpiryReminderScheduler(repository);

        var results = await scheduler.FindDueRemindersAsync(now, CancellationToken.None);

        Assert.Empty(results);
    }

    [Fact]
    public async Task FindDueRemindersAsync_caps_the_number_of_items_returned_at_max_results()
    {
        var now = DateTimeOffset.UtcNow;
        var reminders = Enumerable.Range(0, 3).Select(_ => CreateCandidate(now.AddDays(1))).ToList();
        var repository = new InMemoryInventoryExpiryNotificationRepository(reminders);
        var scheduler = new InventoryExpiryReminderScheduler(repository);

        var results = await scheduler.FindDueRemindersAsync(now, CancellationToken.None, maxResults: 2);

        Assert.Equal(2, results.Count);
    }

    private static DueExpiryReminder CreateCandidate(DateTimeOffset expiryDate)
        => new(Guid.NewGuid(), Guid.NewGuid(), "Milk", expiryDate, NotificationChannel.Push);
}
