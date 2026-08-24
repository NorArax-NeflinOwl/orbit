using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Notifications;
using Xunit;

namespace Orbit.Api.Tests.Notifications;

public sealed class NotificationRecorderTests
{
    private static NotificationRecorder CreateRecorder(
        InMemoryNotificationSettingsRepository? settingsRepository = null, InMemoryNotificationEntryRepository? entryRepository = null)
        => new(settingsRepository ?? new InMemoryNotificationSettingsRepository(), entryRepository ?? new InMemoryNotificationEntryRepository());

    [Fact]
    public async Task RecordAndFilterAsync_records_a_feed_entry_and_allows_every_requested_channel_by_default()
    {
        var entryRepository = new InMemoryNotificationEntryRepository();
        var recorder = CreateRecorder(entryRepository: entryRepository);
        var userId = Guid.NewGuid();

        var result = await recorder.RecordAndFilterAsync(
            userId, NotificationChannel.Both, NotificationEntryKind.PushReminder, "Title", "Body", "/tasks/1", CancellationToken.None);

        Assert.Equal(NotificationChannel.Both, result.AllowedChannel);
        Assert.True(result.EntryRecorded);
        var entries = await entryRepository.GetRecentAsync(userId, 10, CancellationToken.None);
        var entry = Assert.Single(entries);
        Assert.Equal("Title", entry.Title);
        Assert.Equal("Body", entry.Body);
        Assert.Equal("/tasks/1", entry.Url);
        Assert.False(entry.IsRead);
    }

    [Fact]
    public async Task RecordAndFilterAsync_does_not_record_an_entry_or_allow_any_channel_when_the_master_switch_is_off()
    {
        var settingsRepository = new InMemoryNotificationSettingsRepository();
        var entryRepository = new InMemoryNotificationEntryRepository();
        var recorder = CreateRecorder(settingsRepository, entryRepository);
        var userId = Guid.NewGuid();
        var settings = NotificationSettings.Default(userId);
        settings.Update(allowNotifications: false, allowPush: true, allowEmail: true, allowMobileBanner: true, showExceptionDetails: true, BannerTiming.Default);
        await settingsRepository.UpsertAsync(settings, CancellationToken.None);

        var result = await recorder.RecordAndFilterAsync(
            userId, NotificationChannel.Both, NotificationEntryKind.PushReminder, "Title", "Body", null, CancellationToken.None);

        Assert.Equal(NotificationChannel.None, result.AllowedChannel);
        Assert.False(result.EntryRecorded);
        Assert.Empty(await entryRepository.GetRecentAsync(userId, 10, CancellationToken.None));
    }

    [Fact]
    public async Task RecordAndFilterAsync_still_records_an_entry_when_only_the_specific_delivery_channels_are_off()
    {
        var settingsRepository = new InMemoryNotificationSettingsRepository();
        var entryRepository = new InMemoryNotificationEntryRepository();
        var recorder = CreateRecorder(settingsRepository, entryRepository);
        var userId = Guid.NewGuid();
        var settings = NotificationSettings.Default(userId);
        settings.Update(allowNotifications: true, allowPush: false, allowEmail: false, allowMobileBanner: true, showExceptionDetails: true, BannerTiming.Default);
        await settingsRepository.UpsertAsync(settings, CancellationToken.None);

        var result = await recorder.RecordAndFilterAsync(
            userId, NotificationChannel.Both, NotificationEntryKind.PushReminder, "Title", "Body", null, CancellationToken.None);

        Assert.Equal(NotificationChannel.None, result.AllowedChannel);
        Assert.True(result.EntryRecorded);
        Assert.Single(await entryRepository.GetRecentAsync(userId, 10, CancellationToken.None));
    }
}
