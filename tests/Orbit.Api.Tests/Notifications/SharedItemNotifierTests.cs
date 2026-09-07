using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.LiveUpdates;
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

    /// <summary>
    /// The invitation page, which is where an offer can be taken up. It used to lead to the conversation
    /// with whoever sent it, because that is where Accept lived - the item itself is still not the
    /// recipient's to open until they accept, so pointing at it would land on a "not found".
    ///
    /// The sharer's id is in the path as well as the share's: the page names them without a lookup, and
    /// the phone - which reads a closed set of paths and has no invitation screen - takes that last
    /// segment and opens the conversation, exactly where it landed before.
    /// </summary>
    [Fact]
    public async Task An_invitation_leads_to_the_page_that_can_accept_it()
    {
        var context = new SharedItemNotifierTestContext();

        await context.NotifyAsync(SharedItemKind.Inventory, "Pantry");

        Assert.Equal(
            $"/invitation/inventory/{SharedItemNotifierTestContext.ShareId}/{context.SharerId}",
            Assert.Single(await context.RecipientEntriesAsync()).Url);
    }

    /// <summary>
    /// Each kind is named in the path, and the names are stable: they sit in notification rows already
    /// written and in paths already handed to a phone.
    /// </summary>
    [Theory]
    [InlineData(SharedItemKind.Note, "note")]
    [InlineData(SharedItemKind.TaskList, "tasklist")]
    [InlineData(SharedItemKind.CalendarEvent, "event")]
    [InlineData(SharedItemKind.Inventory, "inventory")]
    public async Task Every_kind_of_offer_names_itself_in_the_path(SharedItemKind kind, string expected)
    {
        var context = new SharedItemNotifierTestContext();

        await context.NotifyAsync(kind, "Something");

        Assert.StartsWith($"/invitation/{expected}/", Assert.Single(await context.RecipientEntriesAsync()).Url);
    }

    /// <summary>
    /// Something the recipient already holds opens the thing itself - which is what claiming a public
    /// link produces: the grant is immediate, so there is nothing an invitation page could offer.
    /// </summary>
    [Fact]
    public async Task Something_already_granted_leads_straight_to_it()
    {
        var context = new SharedItemNotifierTestContext();
        var noteId = Guid.NewGuid();

        await context.NotifyAsync(SharedItemKind.Note, "Shopping", SharedItemLink.StraightToIt(noteId));

        Assert.Equal($"/notes/{noteId}", Assert.Single(await context.RecipientEntriesAsync()).Url);
    }

    [Fact]
    public async Task A_shared_position_leads_to_the_map_instead()
    {
        var context = new SharedItemNotifierTestContext();

        await context.NotifyAsync(SharedItemKind.Location, itemTitle: null, SharedItemLink.TheMap);

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

    [Fact]
    public async Task Turning_the_setting_on_also_emails_the_invitation()
    {
        var context = new SharedItemNotifierTestContext();
        await context.AllowShareNotificationsAsync(true);

        await context.NotifyAsync(SharedItemKind.CalendarEvent, "Dentist");

        var email = Assert.Single(context.EmailSender.SentEmails);
        Assert.Equal("bea@example.com", email.ToEmailAddress);
        Assert.Equal("Anna Kowalska shared an event with you", email.Subject);
        Assert.Contains("Name: Dentist", email.Body);
    }

    /// <summary>The email points where the invitation itself does - see SharedItemNotifier.UrlFor.</summary>
    [Fact]
    public async Task The_email_carries_a_link_to_where_the_invitation_leads()
    {
        var context = new SharedItemNotifierTestContext(webClientBaseUrl: "https://orbit.example");
        await context.AllowShareNotificationsAsync(true);

        await context.NotifyAsync(SharedItemKind.Inventory, "Pantry");

        var email = Assert.Single(context.EmailSender.SentEmails);
        Assert.Contains(
            $"https://orbit.example/invitation/inventory/{SharedItemNotifierTestContext.ShareId}/{context.SharerId}",
            email.Body);
    }

    /// <summary>No address configured is the common case on a fresh checkout - see IWebClientLinks.</summary>
    [Fact]
    public async Task The_email_carries_no_link_when_no_public_address_is_configured()
    {
        var context = new SharedItemNotifierTestContext(webClientBaseUrl: null);
        await context.AllowShareNotificationsAsync(true);

        await context.NotifyAsync(SharedItemKind.Inventory, "Pantry");

        var email = Assert.Single(context.EmailSender.SentEmails);
        Assert.DoesNotContain("://", email.Body);
    }

    [Fact]
    public async Task The_invitation_is_not_emailed_while_the_extra_notification_is_off()
    {
        var context = new SharedItemNotifierTestContext();

        await context.NotifyAsync(SharedItemKind.CalendarEvent, "Dentist");

        Assert.Single(await context.RecipientEntriesAsync());
        Assert.Empty(context.EmailSender.SentEmails);
    }

    [Fact]
    public async Task Each_channel_is_asked_for_separately()
    {
        var context = new SharedItemNotifierTestContext();
        await context.SubscribeRecipientToPushAsync();
        await context.AllowChannelsAsync(allowPush: true, allowEmail: false);

        await context.NotifyAsync(SharedItemKind.Note, "Shopping");

        // Share notifications are on, so the extra goes out - but only down the channel this account
        // left switched on.
        Assert.Single(context.PushNotificationSender.SentNotifications);
        Assert.Empty(context.EmailSender.SentEmails);
    }

    [Fact]
    public async Task A_mail_server_that_is_down_does_not_undo_the_share()
    {
        var context = new SharedItemNotifierTestContext(emailSender: new ThrowingEmailSender());
        await context.AllowShareNotificationsAsync(true);

        // The share is already saved by the time this runs, so the announcement must not throw back
        // into the handler that saved it.
        await context.NotifyAsync(SharedItemKind.Note, "Shopping");

        Assert.Single(await context.RecipientEntriesAsync());
    }

    private sealed class SharedItemNotifierTestContext
    {
        private readonly InMemoryNotificationSettingsRepository _settingsRepository = new();
        private readonly InMemoryNotificationEntryRepository _entryRepository = new();
        private readonly InMemoryPushSubscriptionRepository _pushSubscriptionRepository = new();
        private readonly InMemoryUserRepository _userRepository = new();
        private readonly SharedItemNotifier _notifier;

        public RecordingPushNotificationSender PushNotificationSender { get; } = new();
        public RecordingEmailSender EmailSender { get; } = new();
        private readonly IEmailSender _emailSender;
        public Guid SharerId { get; }
        public Guid RecipientId { get; }

        public SharedItemNotifierTestContext(
            bool registerSharer = true, IEmailSender? emailSender = null, string? webClientBaseUrl = null)
        {
            var sharer = User.Create("anna@example.com", "anna", "Anna Kowalska", "hash");
            SharerId = sharer.Id;
            var recipient = User.Create("bea@example.com", "bea", "Bea Nowak", "hash");
            RecipientId = recipient.Id;
            _userRepository.AddAsync(recipient, CancellationToken.None).GetAwaiter().GetResult();
            _emailSender = emailSender ?? EmailSender;
            if (registerSharer)
            {
                _userRepository.AddAsync(sharer, CancellationToken.None).GetAwaiter().GetResult();
            }

            _notifier = new SharedItemNotifier(
                _settingsRepository,
                new NotificationRecorder(_settingsRepository, _entryRepository, new SilentLiveUpdatePublisher()),
                new PushNotificationDispatcher(_pushSubscriptionRepository, [PushNotificationSender], NullLogger<PushNotificationDispatcher>.Instance),
                _userRepository,
                _emailSender,
                new FixedWebClientLinks(webClientBaseUrl),
                NullLogger<SharedItemNotifier>.Instance);
        }

        /// <summary>An offer waiting to be taken up, which is what every share but a position is.</summary>
        public Task NotifyAsync(SharedItemKind kind, string? itemTitle)
            => NotifyAsync(kind, itemTitle, SharedItemLink.ToAccept(ShareId));

        public Task NotifyAsync(SharedItemKind kind, string? itemTitle, SharedItemLink link)
            => _notifier.NotifyAsync(RecipientId, SharerId, kind, itemTitle, link, CancellationToken.None);

        /// <summary>The offer these tests announce, so a test can say where the notification led.</summary>
        public static readonly Guid ShareId = Guid.NewGuid();

        public Task<IReadOnlyList<NotificationEntry>> RecipientEntriesAsync()
            => _entryRepository.GetRecentAsync(RecipientId, 10, CancellationToken.None);

        public Task AllowShareNotificationsAsync(bool allowed) => UpdateSettingsAsync(allowNotifications: true, allowed);

        public Task AllowNotificationsAsync(bool allowed) => UpdateSettingsAsync(allowed, allowShareNotifications: true);

        public Task AllowChannelsAsync(bool allowPush, bool allowEmail)
            => UpdateSettingsAsync(allowNotifications: true, allowShareNotifications: true, allowPush, allowEmail);

        public Task SubscribeRecipientToPushAsync()
            => _pushSubscriptionRepository.AddOrReplaceAsync(
                PushSubscription.CreateForBrowser(RecipientId, new WebPushRegistration("https://push.example/a", "p256dh", "auth")), CancellationToken.None);

        private Task UpdateSettingsAsync(
            bool allowNotifications, bool allowShareNotifications, bool allowPush = true, bool allowEmail = true)
        {
            var settings = NotificationSettings.Default(RecipientId);
            settings.Update(
                allowNotifications, allowPush, allowEmail, allowMobileBanner: true, showExceptionDetails: true,
                allowShareNotifications, BannerTiming.Default, NotificationSettings.DefaultRetentionDays);
            return _settingsRepository.UpsertAsync(settings, CancellationToken.None);
        }
    }

    /// <summary>An unreachable mail server, so the announcement can be shown not to fail the share.</summary>
    private sealed class ThrowingEmailSender : IEmailSender
    {
        public Task SendAsync(string toEmailAddress, string subject, string body, CancellationToken cancellationToken)
            => throw new InvalidOperationException("The mail server is unreachable.");
    }
}
