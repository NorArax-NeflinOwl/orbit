using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Notifications;
using Orbit.Core.Notifications.ClearNotifications;
using Orbit.Core.Notifications.GetNotificationEntries;
using Orbit.Core.Notifications.GetNotificationHistory;
using Orbit.Core.Notifications.MarkNotificationsAtUrlRead;
using Xunit;

namespace Orbit.Api.Tests.Notifications;

/// <summary>
/// Covers what changed about clearing: it tidies the panel rather than destroying the record, the
/// notifications page still lists what was cleared, arriving at a page counts as reading its
/// notification, and the retention window is what actually deletes.
/// </summary>
public sealed class NotificationRetentionTests
{
    [Fact]
    public async Task Clearing_takes_entries_out_of_the_panel()
    {
        var context = new NotificationTestContext();
        await context.RecordAsync("A task is overdue", "/tasks/1");

        await context.ClearAsync();

        Assert.Empty(await context.PanelEntriesAsync());
    }

    [Fact]
    public async Task A_cleared_entry_is_still_there_to_be_found()
    {
        var context = new NotificationTestContext();
        await context.RecordAsync("A task is overdue", "/tasks/1");

        await context.ClearAsync();

        // The whole point of the change: Clear used to delete on the spot, so a notification glanced at
        // and cleared was gone for good.
        var entry = Assert.Single(await context.HistoryAsync());
        Assert.Equal("A task is overdue", entry.Title);
        Assert.True(entry.IsDismissed);
    }

    [Fact]
    public async Task Clearing_also_counts_as_reading()
    {
        var context = new NotificationTestContext();
        await context.RecordAsync("A task is overdue", "/tasks/1");

        await context.ClearAsync();

        // Otherwise the unread badge would stay lit over a panel showing nothing.
        Assert.True(Assert.Single(await context.HistoryAsync()).IsRead);
    }

    [Fact]
    public async Task Arriving_at_a_page_reads_the_notification_that_pointed_there()
    {
        var context = new NotificationTestContext();
        await context.RecordAsync("A task is overdue", "/tasks/1");

        await context.MarkReadAtAsync("/tasks/1");

        Assert.True(Assert.Single(await context.PanelEntriesAsync()).IsRead);
    }

    [Fact]
    public async Task Arriving_somewhere_else_leaves_it_unread()
    {
        var context = new NotificationTestContext();
        await context.RecordAsync("A task is overdue", "/tasks/1");

        await context.MarkReadAtAsync("/tasks/2");

        Assert.False(Assert.Single(await context.PanelEntriesAsync()).IsRead);
    }

    [Fact]
    public async Task Arriving_at_a_page_leaves_the_entry_in_the_panel()
    {
        var context = new NotificationTestContext();
        await context.RecordAsync("A task is overdue", "/tasks/1");

        await context.MarkReadAtAsync("/tasks/1");

        // Read is not the same as cleared: having seen the page doesn't mean the reader wants the entry
        // gone from the list they are about to look at.
        Assert.Single(await context.PanelEntriesAsync());
    }

    [Fact]
    public async Task An_entry_older_than_the_retention_window_is_deleted()
    {
        var context = new NotificationTestContext();
        await context.RecordAsync("Ancient news", "/tasks/1", ageDays: 4);

        await context.SweepAsync();

        Assert.Empty(await context.HistoryAsync());
    }

    [Fact]
    public async Task An_entry_inside_the_window_survives_the_sweep()
    {
        var context = new NotificationTestContext();
        await context.RecordAsync("Yesterday's news", "/tasks/1", ageDays: 1);

        await context.SweepAsync();

        Assert.Single(await context.HistoryAsync());
    }

    [Fact]
    public async Task The_sweep_deletes_a_cleared_entry_and_an_unread_one_alike()
    {
        var context = new NotificationTestContext();
        await context.RecordAsync("Cleared and old", "/tasks/1", ageDays: 4);
        await context.RecordAsync("Unread and old", "/tasks/2", ageDays: 4);
        await context.ClearAsync();

        await context.SweepAsync();

        // Retention is about age, not about what the reader did with it - otherwise an entry nobody ever
        // opened would live forever.
        Assert.Empty(await context.HistoryAsync());
    }

    [Fact]
    public async Task A_longer_window_of_the_readers_own_choosing_is_respected()
    {
        var context = new NotificationTestContext();
        await context.RecordAsync("Last week's news", "/tasks/1", ageDays: 6);
        context.SetRetentionDays(30);

        await context.SweepAsync();

        Assert.Single(await context.HistoryAsync());
    }

    private sealed class NotificationTestContext
    {
        private readonly InMemoryNotificationEntryRepository _entryRepository = new();
        private readonly Guid _userId = Guid.NewGuid();

        public async Task RecordAsync(string title, string url, int ageDays = 0)
        {
            var entry = NotificationEntry.FromPersistence(
                Guid.NewGuid(), _userId, NotificationEntryKind.PushReminder, title, [], "Body", [], url,
                DateTimeOffset.UtcNow.AddDays(-ageDays), readAtUtc: null, dismissedAtUtc: null);
            await _entryRepository.AddAsync(entry, CancellationToken.None);
        }

        public void SetRetentionDays(int retentionDays) => _entryRepository.RetentionDaysByUser[_userId] = retentionDays;

        public Task ClearAsync()
            => new ClearNotificationsCommandHandler(_entryRepository)
                .HandleAsync(new ClearNotificationsCommand(_userId), CancellationToken.None);

        public Task MarkReadAtAsync(string url)
            => new MarkNotificationsAtUrlReadCommandHandler(_entryRepository)
                .HandleAsync(new MarkNotificationsAtUrlReadCommand(_userId, url), CancellationToken.None);

        public Task<IReadOnlyList<NotificationEntry>> PanelEntriesAsync()
            => new GetNotificationEntriesQueryHandler(_entryRepository)
                .HandleAsync(new GetNotificationEntriesQuery(_userId, 30), CancellationToken.None);

        public Task<IReadOnlyList<NotificationEntry>> HistoryAsync()
            => new GetNotificationHistoryQueryHandler(_entryRepository)
                .HandleAsync(new GetNotificationHistoryQuery(_userId, 200), CancellationToken.None);

        public Task<int> SweepAsync()
            => _entryRepository.DeleteExpiredAsync(
                DateTimeOffset.UtcNow, TimeSpan.FromDays(NotificationSettings.DefaultRetentionDays), CancellationToken.None);
    }
}
