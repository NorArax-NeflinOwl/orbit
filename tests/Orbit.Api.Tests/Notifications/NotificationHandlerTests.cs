using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Notifications;
using Orbit.Core.Notifications.GetNotificationEntries;
using Orbit.Core.Notifications.GetNotificationSettings;
using Orbit.Core.Notifications.GetUnreadNotificationCount;
using Orbit.Core.Notifications.MarkAllNotificationsRead;
using Orbit.Core.Notifications.UpdateNotificationSettings;
using Xunit;

namespace Orbit.Api.Tests.Notifications;

public sealed class NotificationHandlerTests
{
    [Fact]
    public async Task GetNotificationSettingsQueryHandler_returns_defaults_for_a_user_with_no_stored_settings()
    {
        var handler = new GetNotificationSettingsQueryHandler(new InMemoryNotificationSettingsRepository());
        var userId = Guid.NewGuid();

        var settings = await handler.HandleAsync(new GetNotificationSettingsQuery(userId), CancellationToken.None);

        Assert.Equal(userId, settings.UserId);
        Assert.True(settings.AllowNotifications);
    }

    [Fact]
    public async Task UpdateNotificationSettingsCommandHandler_persists_and_returns_the_updated_settings()
    {
        var repository = new InMemoryNotificationSettingsRepository();
        var handler = new UpdateNotificationSettingsCommandHandler(repository);
        var userId = Guid.NewGuid();

        var updated = await handler.HandleAsync(
            new UpdateNotificationSettingsCommand(userId, AllowNotifications: true, AllowPush: false, AllowEmail: true, AllowMobileBanner: false, ShowExceptionDetails: false, BannerTiming.Default),
            CancellationToken.None);

        Assert.False(updated.AllowPush);
        Assert.True(updated.AllowEmail);
        var stored = await repository.GetAsync(userId, CancellationToken.None);
        Assert.False(stored.AllowPush);
        Assert.False(stored.AllowMobileBanner);
    }

    [Fact]
    public async Task GetNotificationEntriesQueryHandler_returns_only_the_requesting_users_entries()
    {
        var repository = new InMemoryNotificationEntryRepository();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        await repository.AddAsync(NotificationEntry.Create(userId, NotificationEntryKind.PushReminder, "Mine", "Body", null), CancellationToken.None);
        await repository.AddAsync(NotificationEntry.Create(otherUserId, NotificationEntryKind.PushReminder, "Not mine", "Body", null), CancellationToken.None);
        var handler = new GetNotificationEntriesQueryHandler(repository);

        var entries = await handler.HandleAsync(new GetNotificationEntriesQuery(userId, 30), CancellationToken.None);

        Assert.Equal("Mine", Assert.Single(entries).Title);
    }

    [Fact]
    public async Task GetUnreadNotificationCountQueryHandler_counts_only_unread_entries()
    {
        var repository = new InMemoryNotificationEntryRepository();
        var userId = Guid.NewGuid();
        var readEntry = NotificationEntry.Create(userId, NotificationEntryKind.PushReminder, "Read", "Body", null);
        readEntry.MarkRead(DateTimeOffset.UtcNow);
        await repository.AddAsync(readEntry, CancellationToken.None);
        await repository.AddAsync(NotificationEntry.Create(userId, NotificationEntryKind.PushReminder, "Unread", "Body", null), CancellationToken.None);
        var handler = new GetUnreadNotificationCountQueryHandler(repository);

        var count = await handler.HandleAsync(new GetUnreadNotificationCountQuery(userId), CancellationToken.None);

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task MarkAllNotificationsReadCommandHandler_marks_every_unread_entry_for_the_user_as_read()
    {
        var repository = new InMemoryNotificationEntryRepository();
        var userId = Guid.NewGuid();
        await repository.AddAsync(NotificationEntry.Create(userId, NotificationEntryKind.PushReminder, "One", "Body", null), CancellationToken.None);
        await repository.AddAsync(NotificationEntry.Create(userId, NotificationEntryKind.ChatMessage, "Two", "Body", null), CancellationToken.None);
        var handler = new MarkAllNotificationsReadCommandHandler(repository);

        var result = await handler.HandleAsync(new MarkAllNotificationsReadCommand(userId), CancellationToken.None);

        Assert.True(result);
        Assert.Equal(0, await repository.GetUnreadCountAsync(userId, CancellationToken.None));
    }
}
