using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Notifications;
using Orbit.Core.Notifications.ClearNotifications;
using Orbit.Core.Notifications.GetChangedNotifications;
using Orbit.Core.Notifications.MarkNotificationsAtUrlRead;
using Xunit;

namespace Orbit.Api.Tests.Notifications;

/// <summary>
/// The delta a phone pulls so it can hold its own copy of the feed and show it with no connection - the
/// same shape notes, task lists, calendar events and warehouses already answer in.
///
/// The point that makes it more than "what is new": reading one and clearing one both change what the
/// feed shows. A client told only about new entries would keep showing an old one as unread forever.
/// </summary>
public sealed class NotificationChangeFeedTests
{
    [Fact]
    public async Task Something_recorded_after_the_cursor_comes_back()
    {
        var context = new ChangeFeedContext();
        var before = DateTimeOffset.UtcNow;
        await context.RecordAsync("A task is overdue", "/tasks/1");

        var changed = await context.ChangedSinceAsync(before);

        Assert.Equal("A task is overdue", Assert.Single(changed).Title);
    }

    [Fact]
    public async Task Something_recorded_before_the_cursor_does_not()
    {
        var context = new ChangeFeedContext();
        await context.RecordAsync("A task is overdue", "/tasks/1");

        var changed = await context.ChangedSinceAsync(DateTimeOffset.UtcNow);

        Assert.Empty(changed);
    }

    /// <summary>
    /// Reading is a change. Without this a phone that pulled an entry while it was unread would go on
    /// badging it forever, because nothing new ever happened to it.
    /// </summary>
    [Fact]
    public async Task Reading_one_brings_it_back_as_changed()
    {
        var context = new ChangeFeedContext();
        await context.RecordAsync("A task is overdue", "/tasks/1");
        var afterItWasRecorded = DateTimeOffset.UtcNow;

        await context.MarkReadAtAsync("/tasks/1");

        var entry = Assert.Single(await context.ChangedSinceAsync(afterItWasRecorded));
        Assert.True(entry.IsRead);
    }

    /// <summary>Clearing is the other one: the phone has to be told the entry left the panel.</summary>
    [Fact]
    public async Task Clearing_one_brings_it_back_as_changed()
    {
        var context = new ChangeFeedContext();
        await context.RecordAsync("A task is overdue", "/tasks/1");
        var afterItWasRecorded = DateTimeOffset.UtcNow;

        await context.ClearAsync();

        var entry = Assert.Single(await context.ChangedSinceAsync(afterItWasRecorded));
        Assert.True(entry.IsDismissed);
    }

    [Fact]
    public async Task Somebody_else_s_notifications_are_never_in_it()
    {
        var context = new ChangeFeedContext();
        var before = DateTimeOffset.UtcNow;
        await context.RecordForSomebodyElseAsync("Not yours", "/tasks/1");

        Assert.Empty(await context.ChangedSinceAsync(before));
    }

    private sealed class ChangeFeedContext
    {
        private readonly InMemoryNotificationEntryRepository _entryRepository = new();
        private readonly Guid _userId = Guid.NewGuid();

        public Task RecordAsync(string title, string url) => RecordForAsync(_userId, title, url);

        public Task RecordForSomebodyElseAsync(string title, string url) => RecordForAsync(Guid.NewGuid(), title, url);

        private async Task RecordForAsync(Guid userId, string title, string url)
        {
            var entry = NotificationEntry.FromPersistence(
                Guid.NewGuid(), userId, NotificationEntryKind.PushReminder, title, [], "Body", [], url,
                DateTimeOffset.UtcNow, readAtUtc: null, dismissedAtUtc: null);
            await _entryRepository.AddAsync(entry, CancellationToken.None);
        }

        public Task ClearAsync()
            => new ClearNotificationsCommandHandler(_entryRepository)
                .HandleAsync(new ClearNotificationsCommand(_userId), CancellationToken.None);

        public Task MarkReadAtAsync(string url)
            => new MarkNotificationsAtUrlReadCommandHandler(_entryRepository)
                .HandleAsync(new MarkNotificationsAtUrlReadCommand(_userId, url), CancellationToken.None);

        public Task<IReadOnlyList<NotificationEntry>> ChangedSinceAsync(DateTimeOffset since)
            => new GetChangedNotificationsQueryHandler(_entryRepository)
                .HandleAsync(new GetChangedNotificationsQuery(_userId, since, 200), CancellationToken.None);
    }
}
