using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Notifications;
using Orbit.Core.Users;
using Xunit;

namespace Orbit.Api.Tests.Notifications;

/// <summary>
/// Covers the split this feature rests on: the invitation in the notification feed is the point and
/// always arrives, while the push on top is the optional extra that starts switched off.
/// </summary>
public sealed class SharedItemNotifierTests
{
    [Fact]
    public async Task Sharing_something_leaves_an_invitation_in_the_recipients_feed()
    {
        var context = new SharedItemNotifierTestContext();

        await context.NotifyAsync(SharedItemKind.Note, "Shopping");

        var entry = Assert.Single(await context.RecipientEntriesAsync());
        // The entry holds the sentence with a hole in it and what goes in the hole, so the client can
        // say it in the reader's language - see PushNotificationPayload. Read back together, it is the
        // sentence it always was.
        Assert.Equal("{0} shared a note with you", entry.Title);
        Assert.Equal(["Anna Kowalska"], entry.TitleArguments);
        // The body is the reader's own words for their own thing, so it is all argument and no sentence.
        Assert.Equal(["Shopping"], entry.BodyArguments);
        Assert.Equal(NotificationEntryKind.SharedWithYou, entry.Kind);
        Assert.False(entry.IsRead);
    }

    [Fact]
    public async Task The_invitation_arrives_even_though_the_extra_notification_is_off_by_default()
    {
        var context = new SharedItemNotifierTestContext();

        await context.NotifyAsync(SharedItemKind.TaskList, "Groceries");

        // Nobody has touched the settings, so AllowShareNotifications is at its default of false. The
        // feed entry is not what that switch governs - it is the invitation itself.
        Assert.Single(await context.RecipientEntriesAsync());
        Assert.Empty(context.PushNotificationSender.SentNotifications);
    }

    [Fact]
    public async Task Turning_the_setting_on_adds_a_push_on_top_of_the_invitation()
    {
        var context = new SharedItemNotifierTestContext();
        await context.AllowShareNotificationsAsync(true);
        await context.SubscribeRecipientToPushAsync();

        await context.NotifyAsync(SharedItemKind.CalendarEvent, "Dentist");

        Assert.Single(await context.RecipientEntriesAsync());
        var sent = Assert.Single(context.PushNotificationSender.SentNotifications);
        Assert.Equal("Anna Kowalska shared an event with you", sent.Payload.Title);
        Assert.Equal("Dentist", sent.Payload.Body);
    }

    [Fact]
    public async Task Switching_notifications_off_altogether_silences_the_invitation_too()
    {
        var context = new SharedItemNotifierTestContext();
        await context.SubscribeRecipientToPushAsync();
        await context.AllowNotificationsAsync(false);

        await context.NotifyAsync(SharedItemKind.Note, "Shopping");

        // The master switch outranks everything, including an invitation the recipient would otherwise
        // always see.
        Assert.Empty(await context.RecipientEntriesAsync());
        Assert.Empty(context.PushNotificationSender.SentNotifications);
    }

    [Fact]
    public async Task An_invitation_leads_to_the_conversation_that_can_accept_it()
    {
        var context = new SharedItemNotifierTestContext();

        await context.NotifyAsync(SharedItemKind.Warehouse, "Pantry");

        // The item isn't the recipient's to open until they accept, and Accept lives in the chat with
        // whoever sent it - pointing at the warehouse itself would land on a "not found".
        Assert.Equal($"/chat/{context.SharerId}", Assert.Single(await context.RecipientEntriesAsync()).Url);
    }

    [Fact]
    public async Task A_shared_position_leads_to_the_map_instead()
    {
        var context = new SharedItemNotifierTestContext();

        await context.NotifyAsync(SharedItemKind.Location, itemTitle: null);

        var entry = Assert.Single(await context.RecipientEntriesAsync());
        Assert.Equal("/map", entry.Url);
        // With no title of its own, the body repeats the headline rather than being left blank.
        Assert.Equal("{0} shared their location with you", entry.Body);
        Assert.Equal(["Anna Kowalska"], entry.BodyArguments);
    }

    [Fact]
    public async Task A_sharer_who_no_longer_exists_still_produces_a_readable_invitation()
    {
        var context = new SharedItemNotifierTestContext(registerSharer: false);

        await context.NotifyAsync(SharedItemKind.Note, "Shopping");

        var entry = Assert.Single(await context.RecipientEntriesAsync());
        Assert.Equal("{0} shared a note with you", entry.Title);
        Assert.Equal(["Someone"], entry.TitleArguments);
    }

    private sealed class SharedItemNotifierTestContext
    {
        private readonly InMemoryNotificationSettingsRepository _settingsRepository = new();
        private readonly InMemoryNotificationEntryRepository _entryRepository = new();
        private readonly InMemoryPushSubscriptionRepository _pushSubscriptionRepository = new();
        private readonly InMemoryUserRepository _userRepository = new();
        private readonly SharedItemNotifier _notifier;

        public RecordingPushNotificationSender PushNotificationSender { get; } = new();
        public Guid SharerId { get; }
        public Guid RecipientId { get; } = Guid.NewGuid();

        public SharedItemNotifierTestContext(bool registerSharer = true)
        {
            var sharer = User.Create("anna@example.com", "anna", "Anna Kowalska", "hash");
            SharerId = sharer.Id;
            if (registerSharer)
            {
                _userRepository.AddAsync(sharer, CancellationToken.None).GetAwaiter().GetResult();
            }

            _notifier = new SharedItemNotifier(
                _settingsRepository,
                new NotificationRecorder(_settingsRepository, _entryRepository),
                new PushNotificationDispatcher(_pushSubscriptionRepository, [PushNotificationSender], NullLogger<PushNotificationDispatcher>.Instance),
                _userRepository);
        }

        public Task NotifyAsync(SharedItemKind kind, string? itemTitle)
            => _notifier.NotifyAsync(RecipientId, SharerId, kind, itemTitle, CancellationToken.None);

        public Task<IReadOnlyList<NotificationEntry>> RecipientEntriesAsync()
            => _entryRepository.GetRecentAsync(RecipientId, 10, CancellationToken.None);

        public Task AllowShareNotificationsAsync(bool allowed) => UpdateSettingsAsync(allowNotifications: true, allowed);

        public Task AllowNotificationsAsync(bool allowed) => UpdateSettingsAsync(allowed, allowShareNotifications: true);

        public Task SubscribeRecipientToPushAsync()
            => _pushSubscriptionRepository.AddOrReplaceAsync(
                PushSubscription.CreateForBrowser(RecipientId, new WebPushRegistration("https://push.example/a", "p256dh", "auth")), CancellationToken.None);

        private Task UpdateSettingsAsync(bool allowNotifications, bool allowShareNotifications)
        {
            var settings = NotificationSettings.Default(RecipientId);
            settings.Update(
                allowNotifications, allowPush: true, allowEmail: true, allowMobileBanner: true, showExceptionDetails: true,
                allowShareNotifications, BannerTiming.Default, NotificationSettings.DefaultRetentionDays);
            return _settingsRepository.UpsertAsync(settings, CancellationToken.None);
        }
    }
}
