using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Calendar;
using Orbit.Core.Calendar.Reminders;
using Orbit.Core.Chat.Groups.ManageChatGroupMembers;
using Orbit.Core.Chat.SendMessage;
using Orbit.Core.Inventory.ExpiryReminders;
using Orbit.Core.LiveUpdates;
using Orbit.Core.Notifications;
using Orbit.Core.Tasks.DailyReminders;
using Orbit.Core.Tasks.OverdueNotifications;
using Orbit.Core.Users;
using Orbit.Localization;
using Xunit;

namespace Orbit.Api.Tests.Notifications;

/// <summary>
/// What a notification says, and in whose language.
///
/// The server used to finish these sentences itself, in English, and both clients showed the result - so
/// a Polish reader got "New message from Dev Three" under a Polish heading. It cannot finish them: the
/// language is a preference each device keeps for itself and nothing ever tells the server about it. So
/// it sends the sentence and what fills it, and the client says it.
///
/// These tests are about the sentence surviving that split: every format has to be a key the shared
/// dictionary knows, or the clients would look up something that is not there and quietly show English
/// again - which is the failure this whole change was about.
/// </summary>
public sealed class NotificationWordingTests
{
    /// <summary>Every sentence the server can write, from every place that writes one.</summary>
    public static TheoryData<PushNotificationPayload> EverySentence()
    {
        var details = new CalendarEventDetails(
            "Dentist", null, null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), false,
            null, [], [], NotificationChannel.None);

        return
        [
            ChatMessagePushContent.Build(Guid.NewGuid(), "Bea"),
            // All three shapes a reminder takes: as it starts, hours before, minutes before.
            EventReminderPushContent.Build(details, Guid.NewGuid(), 0),
            EventReminderPushContent.Build(details, Guid.NewGuid(), 120),
            EventReminderPushContent.Build(details, Guid.NewGuid(), 15),
            ChatGroupInvitationPushContent.Build(Guid.NewGuid(), "Weekend trip", "Bea"),
            InventoryExpiryPushContent.Build(new DueExpiryReminder(
                Guid.NewGuid(), Guid.NewGuid(), "Milk", DateTimeOffset.UtcNow, NotificationChannel.Push)),
            DailyTaskReminderPushContent.Build(new DueDailyTaskReminder(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Groceries", "Buy milk", null,
                NotificationChannel.Push, DateOnly.FromDateTime(DateTime.UtcNow))),
            OverdueTaskPushContent.Build(new OverdueTaskItem(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Groceries", "Buy milk", DateTimeOffset.UtcNow)),
            .. SharingSentences()
        ];
    }

    /// <summary>
    /// Every sentence with a word in it is a key the dictionary knows. A format nobody translated falls
    /// back to the English it already is - correct, but silently English, which is exactly how this went
    /// unnoticed for so long.
    ///
    /// A format that is nothing but a placeholder is skipped: SharedItemNotifier's body is "{0}" and all
    /// of what it says is the reader's own name for their own thing. There is nothing there to translate,
    /// and the fallback renders it right.
    /// </summary>
    [Theory]
    [MemberData(nameof(EverySentence))]
    public void Every_sentence_the_server_writes_can_be_said_in_polish(PushNotificationPayload says)
    {
        AssertTranslated(says.TitleFormat);
        AssertTranslated(says.BodyFormat);
    }

    /// <summary>
    /// And the Polish has the same holes in it as the English. A translation that dropped a {0} would
    /// lose the name or the title out of the sentence; one that invented a {2} would throw when it was
    /// formatted, on a screen somebody had opened to find out what had happened.
    /// </summary>
    [Theory]
    [MemberData(nameof(EverySentence))]
    public void The_polish_has_a_place_for_everything_the_english_does(PushNotificationPayload says)
    {
        AssertSameHoles(says.TitleFormat, says.TitleArguments.Count);
        AssertSameHoles(says.BodyFormat, says.BodyArguments.Count);
    }

    /// <summary>
    /// What reaches a push notification is still the finished English sentence: Android and the browser's
    /// service worker draw those themselves, and the server has no language to write them in anyway.
    /// Worth pinning down, because it is the half that deliberately did not change.
    /// </summary>
    [Fact]
    public void A_push_still_carries_a_finished_sentence()
    {
        var says = ChatMessagePushContent.Build(Guid.NewGuid(), "Bea");

        Assert.Equal("New message", says.Title);
        Assert.Equal("New message from Bea", says.Body);
    }

    /// <summary>
    /// The five ways something can be shared. SharedItemNotifier writes a whole sentence per kind rather
    /// than dropping "a note" into a common one, because Polish declines the shared noun - so each kind
    /// is its own sentence, and each could go untranslated on its own. Driven through the real notifier
    /// because those sentences live inside it and nothing else can be asked for them.
    /// </summary>
    private static IEnumerable<PushNotificationPayload> SharingSentences()
    {
        foreach (var kind in Enum.GetValues<SharedItemKind>())
        {
            var settingsRepository = new InMemoryNotificationSettingsRepository();
            var entryRepository = new InMemoryNotificationEntryRepository();
            var userRepository = new InMemoryUserRepository();
            var sharer = User.Create("anna@example.com", "anna", "Anna Kowalska", "hash");
            userRepository.AddAsync(sharer, CancellationToken.None).GetAwaiter().GetResult();

            var notifier = new SharedItemNotifier(
                settingsRepository,
                new NotificationRecorder(settingsRepository, entryRepository, new SilentLiveUpdatePublisher()),
                new PushNotificationDispatcher(
                    new InMemoryPushSubscriptionRepository(), [], NullLogger<PushNotificationDispatcher>.Instance),
                userRepository,
                new RecordingEmailSender(),
                new FixedWebClientLinks(),
                NullLogger<SharedItemNotifier>.Instance);

            var recipientId = Guid.NewGuid();
            notifier.NotifyAsync(recipientId, sharer.Id, kind, "Shopping", CancellationToken.None)
                .GetAwaiter().GetResult();

            var entry = entryRepository.GetRecentAsync(recipientId, 1, CancellationToken.None)
                .GetAwaiter().GetResult()[0];
            yield return new PushNotificationPayload(
                entry.Title, entry.TitleArguments, entry.Body, entry.BodyArguments, entry.Url ?? string.Empty);
        }
    }

    private static void AssertTranslated(string english)
    {
        if (!HasWords(english))
        {
            return;
        }

        Assert.True(
            PolishTranslations.ByEnglish.ContainsKey(english),
            $"No Polish for \"{english}\".");
    }

    private static void AssertSameHoles(string english, int argumentCount)
    {
        if (!PolishTranslations.ByEnglish.TryGetValue(english, out var polish))
        {
            return;
        }

        for (var hole = 0; hole < argumentCount; hole++)
        {
            Assert.Contains($"{{{hole}}}", polish);
        }

        Assert.DoesNotContain($"{{{argumentCount}}}", polish);
    }

    /// <summary>Whether there is anything here to translate, or only placeholders and punctuation.</summary>
    private static bool HasWords(string format) => format.Any(char.IsLetter);
}
