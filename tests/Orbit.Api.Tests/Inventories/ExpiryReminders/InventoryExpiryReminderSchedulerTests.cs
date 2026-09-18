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

    /// <summary>
    /// Nothing goes off when there is none of it. Reported on 2026-09-18: a shelf whose rows had been
    /// counted down to zero went on warning that they were nearing their date. A row at zero is a
    /// product the shelf remembers rather than one it holds.
    /// </summary>
    [Fact]
    public async Task FindDueRemindersAsync_does_not_warn_about_a_row_the_shelf_holds_none_of()
    {
        var now = DateTimeOffset.UtcNow;
        var noneLeft = CreateCandidate(expiryDate: now.AddDays(1), quantity: 0);
        var repository = new InMemoryInventoryExpiryNotificationRepository([noneLeft]);
        var scheduler = new InventoryExpiryReminderScheduler(repository);

        var results = await scheduler.FindDueRemindersAsync(now, CancellationToken.None);

        Assert.Empty(results);
    }

    /// <summary>And the same row once it has been restocked, with no reset needed.</summary>
    [Fact]
    public async Task FindDueRemindersAsync_warns_about_that_row_again_once_there_is_some_of_it()
    {
        var now = DateTimeOffset.UtcNow;
        var restocked = CreateCandidate(expiryDate: now.AddDays(1), quantity: 2);
        var repository = new InMemoryInventoryExpiryNotificationRepository([restocked]);
        var scheduler = new InventoryExpiryReminderScheduler(repository);

        var results = await scheduler.FindDueRemindersAsync(now, CancellationToken.None);

        Assert.Equal(restocked, Assert.Single(results));
    }

    /// <summary>Half a bottle is still something that can go off.</summary>
    [Fact]
    public async Task FindDueRemindersAsync_warns_about_a_row_holding_less_than_one()
    {
        var now = DateTimeOffset.UtcNow;
        var halfLeft = CreateCandidate(expiryDate: now.AddDays(1), quantity: 0.5m);
        var repository = new InMemoryInventoryExpiryNotificationRepository([halfLeft]);
        var scheduler = new InventoryExpiryReminderScheduler(repository);

        var results = await scheduler.FindDueRemindersAsync(now, CancellationToken.None);

        Assert.Equal(halfLeft, Assert.Single(results));
    }

    private static DueExpiryReminder CreateCandidate(DateTimeOffset expiryDate, decimal quantity = 1)
        => new(
            Guid.NewGuid(), InventoryId: Guid.NewGuid(), Guid.NewGuid(), "Milk", expiryDate,
            NotificationChannel.Push, quantity);
}
