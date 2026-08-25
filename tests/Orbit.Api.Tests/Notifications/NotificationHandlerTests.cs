using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Notifications;
using Orbit.Core.Notifications.GetNotificationEntries;
using Orbit.Core.Notifications.GetNotificationSettings;
using Orbit.Core.Notifications.GetUnreadNotificationEntries;
using Orbit.Core.Notifications.ClearNotifications;
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
            new UpdateNotificationSettingsCommand(userId, AllowNotifications: true, AllowPush: false, AllowEmail: true, AllowMobileBanner: false, ShowExceptionDetails: false, AllowShareNotifications: false, BannerTiming.Default),
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
    public async Task GetUnreadNotificationEntriesQueryHandler_returns_only_unread_entries()
    {
        var repository = new InMemoryNotificationEntryRepository();
        var userId = Guid.NewGuid();
        var readEntry = NotificationEntry.Create(userId, NotificationEntryKind.PushReminder, "Read", "Body", null);
        readEntry.MarkRead(DateTimeOffset.UtcNow);
        await repository.AddAsync(readEntry, CancellationToken.None);
        await repository.AddAsync(
            NotificationEntry.Create(userId, NotificationEntryKind.ChatMessage, "Unread", "Body", "/chat/abc"), CancellationToken.None);
        var handler = new GetUnreadNotificationEntriesQueryHandler(repository);

        var entries = await handler.HandleAsync(new GetUnreadNotificationEntriesQuery(userId, Take: 30), CancellationToken.None);

        var unread = Assert.Single(entries);
        Assert.Equal("Unread", unread.Title);
        // The Url comes back too - it is what the client badges the individual chat/nav sections by.
        Assert.Equal("/chat/abc", unread.Url);
    }

    [Fact]
    public async Task ClearNotificationsCommandHandler_removes_every_entry_for_the_user_but_leaves_other_users_alone()
    {
        var repository = new InMemoryNotificationEntryRepository();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        await repository.AddAsync(NotificationEntry.Create(userId, NotificationEntryKind.PushReminder, "Mine", "Body", null), CancellationToken.None);
        await repository.AddAsync(NotificationEntry.Create(otherUserId, NotificationEntryKind.PushReminder, "Theirs", "Body", null), CancellationToken.None);
        var handler = new ClearNotificationsCommandHandler(repository);

        await handler.HandleAsync(new ClearNotificationsCommand(userId), CancellationToken.None);

        Assert.Empty(await repository.GetRecentAsync(userId, 30, CancellationToken.None));
        Assert.Single(await repository.GetRecentAsync(otherUserId, 30, CancellationToken.None));
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
        Assert.Empty(await repository.GetUnreadAsync(userId, 30, CancellationToken.None));
    }
}
