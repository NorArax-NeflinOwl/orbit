using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Chat.SendMessage;
using Orbit.Core.Notifications;
using Orbit.Core.Users;
using Xunit;

namespace Orbit.Api.Tests.Chat;

public sealed class SendMessageCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_stores_the_message_and_creates_contacts_in_both_directions()
    {
        var userRepository = new InMemoryUserRepository();
        var recipient = User.FromPersistence(Guid.NewGuid(), "recipient@example.com", "recipient", "Recipient", "hash", DateTimeOffset.UtcNow, null);
        await userRepository.AddAsync(recipient, CancellationToken.None);
        var messageRepository = new InMemoryChatMessageRepository();
        var contactRepository = new InMemoryContactRepository();
        var handler = new SendMessageCommandHandler(
            userRepository, messageRepository, contactRepository, new InMemoryChatConversationAccessRepository(), CreateDispatcher(), CreateNotificationRecorder());
        var senderId = Guid.NewGuid();

        var result = await handler.HandleAsync(
            new SendMessageCommand(senderId, recipient.Id, "ciphertext", "nonce"), CancellationToken.None);

        Assert.Equal(SendMessageOutcome.Success, result.Outcome);
        Assert.Equal("ciphertext", result.Message!.CiphertextBase64);

        var senderContacts = await contactRepository.GetAllForUserAsync(senderId, CancellationToken.None);
        var recipientContacts = await contactRepository.GetAllForUserAsync(recipient.Id, CancellationToken.None);
        Assert.Equal(recipient.Id, Assert.Single(senderContacts).ContactUserId);
        Assert.Equal(senderId, Assert.Single(recipientContacts).ContactUserId);
    }

    [Fact]
    public async Task HandleAsync_returns_RecipientNotFound_when_the_recipient_does_not_exist()
    {
        var handler = new SendMessageCommandHandler(
            new InMemoryUserRepository(), new InMemoryChatMessageRepository(), new InMemoryContactRepository(),
            new InMemoryChatConversationAccessRepository(), CreateDispatcher(), CreateNotificationRecorder());

        var result = await handler.HandleAsync(
            new SendMessageCommand(Guid.NewGuid(), Guid.NewGuid(), "ciphertext", "nonce"), CancellationToken.None);

        Assert.Equal(SendMessageOutcome.RecipientNotFound, result.Outcome);
        Assert.Null(result.Message);
    }

    [Fact]
    public async Task HandleAsync_reuses_the_existing_contact_on_a_second_message()
    {
        var userRepository = new InMemoryUserRepository();
        var recipient = User.FromPersistence(Guid.NewGuid(), "recipient@example.com", "recipient", "Recipient", "hash", DateTimeOffset.UtcNow, null);
        await userRepository.AddAsync(recipient, CancellationToken.None);
        var contactRepository = new InMemoryContactRepository();
        var handler = new SendMessageCommandHandler(
            userRepository, new InMemoryChatMessageRepository(), contactRepository, new InMemoryChatConversationAccessRepository(),
            CreateDispatcher(), CreateNotificationRecorder());
        var senderId = Guid.NewGuid();

        await handler.HandleAsync(new SendMessageCommand(senderId, recipient.Id, "first", "nonce"), CancellationToken.None);
        await handler.HandleAsync(new SendMessageCommand(senderId, recipient.Id, "second", "nonce"), CancellationToken.None);

        var senderContacts = await contactRepository.GetAllForUserAsync(senderId, CancellationToken.None);
        Assert.Single(senderContacts);
    }

    [Fact]
    public async Task HandleAsync_lets_the_initiator_send_more_messages_before_the_other_party_approves()
    {
        var userRepository = new InMemoryUserRepository();
        var recipient = User.FromPersistence(Guid.NewGuid(), "recipient@example.com", "recipient", "Recipient", "hash", DateTimeOffset.UtcNow, null);
        await userRepository.AddAsync(recipient, CancellationToken.None);
        var handler = new SendMessageCommandHandler(
            userRepository, new InMemoryChatMessageRepository(), new InMemoryContactRepository(), new InMemoryChatConversationAccessRepository(),
            CreateDispatcher(), CreateNotificationRecorder());
        var senderId = Guid.NewGuid();

        await handler.HandleAsync(new SendMessageCommand(senderId, recipient.Id, "first", "nonce"), CancellationToken.None);
        var result = await handler.HandleAsync(new SendMessageCommand(senderId, recipient.Id, "second", "nonce"), CancellationToken.None);

        Assert.Equal(SendMessageOutcome.Success, result.Outcome);
    }

    [Fact]
    public async Task HandleAsync_blocks_the_non_initiating_party_from_replying_before_they_approve()
    {
        var userRepository = new InMemoryUserRepository();
        var initiator = User.FromPersistence(Guid.NewGuid(), "initiator@example.com", "initiator", "Initiator", "hash", DateTimeOffset.UtcNow, null);
        var recipient = User.FromPersistence(Guid.NewGuid(), "recipient@example.com", "recipient", "Recipient", "hash", DateTimeOffset.UtcNow, null);
        await userRepository.AddAsync(initiator, CancellationToken.None);
        await userRepository.AddAsync(recipient, CancellationToken.None);
        var handler = new SendMessageCommandHandler(
            userRepository, new InMemoryChatMessageRepository(), new InMemoryContactRepository(), new InMemoryChatConversationAccessRepository(),
            CreateDispatcher(), CreateNotificationRecorder());

        await handler.HandleAsync(new SendMessageCommand(initiator.Id, recipient.Id, "first", "nonce"), CancellationToken.None);
        var result = await handler.HandleAsync(new SendMessageCommand(recipient.Id, initiator.Id, "reply", "nonce"), CancellationToken.None);

        Assert.Equal(SendMessageOutcome.ConversationNotApproved, result.Outcome);
        Assert.Null(result.Message);
    }

    [Fact]
    public async Task HandleAsync_lets_the_non_initiating_party_reply_once_they_have_approved()
    {
        var userRepository = new InMemoryUserRepository();
        var initiator = User.FromPersistence(Guid.NewGuid(), "initiator@example.com", "initiator", "Initiator", "hash", DateTimeOffset.UtcNow, null);
        var recipient = User.FromPersistence(Guid.NewGuid(), "recipient@example.com", "recipient", "Recipient", "hash", DateTimeOffset.UtcNow, null);
        await userRepository.AddAsync(initiator, CancellationToken.None);
        await userRepository.AddAsync(recipient, CancellationToken.None);
        var conversationAccessRepository = new InMemoryChatConversationAccessRepository();
        var handler = new SendMessageCommandHandler(
            userRepository, new InMemoryChatMessageRepository(), new InMemoryContactRepository(), conversationAccessRepository,
            CreateDispatcher(), CreateNotificationRecorder());

        await handler.HandleAsync(new SendMessageCommand(initiator.Id, recipient.Id, "first", "nonce"), CancellationToken.None);
        await conversationAccessRepository.ApproveAsync(recipient.Id, initiator.Id, CancellationToken.None);
        var result = await handler.HandleAsync(new SendMessageCommand(recipient.Id, initiator.Id, "reply", "nonce"), CancellationToken.None);

        Assert.Equal(SendMessageOutcome.Success, result.Outcome);
    }

    [Fact]
    public async Task HandleAsync_sends_the_recipient_a_push_notification_naming_the_sender()
    {
        var userRepository = new InMemoryUserRepository();
        var sender = User.FromPersistence(Guid.NewGuid(), "sender@example.com", "sender", "Ada Lovelace", "hash", DateTimeOffset.UtcNow, null);
        var recipient = User.FromPersistence(Guid.NewGuid(), "recipient@example.com", "recipient", "Recipient", "hash", DateTimeOffset.UtcNow, null);
        await userRepository.AddAsync(sender, CancellationToken.None);
        await userRepository.AddAsync(recipient, CancellationToken.None);
        var subscriptionRepository = new InMemoryPushSubscriptionRepository();
        var subscription = PushSubscription.CreateForBrowser(recipient.Id, new WebPushRegistration("https://push.example/a", "p256dh", "auth"));
        await subscriptionRepository.AddOrReplaceAsync(subscription, CancellationToken.None);
        var pushSender = new RecordingPushNotificationSender();
        var dispatcher = new PushNotificationDispatcher(subscriptionRepository, [pushSender], NullLogger<PushNotificationDispatcher>.Instance);
        var handler = new SendMessageCommandHandler(
            userRepository, new InMemoryChatMessageRepository(), new InMemoryContactRepository(), new InMemoryChatConversationAccessRepository(),
            dispatcher, CreateNotificationRecorder());

        await handler.HandleAsync(new SendMessageCommand(sender.Id, recipient.Id, "ciphertext", "nonce"), CancellationToken.None);

        var sent = Assert.Single(pushSender.SentNotifications);
        Assert.Contains("Ada Lovelace", sent.Payload.Body);
    }

    [Fact]
    public async Task HandleAsync_leaves_a_share_invitation_to_announce_itself()
    {
        var userRepository = new InMemoryUserRepository();
        var sender = User.FromPersistence(Guid.NewGuid(), "sender@example.com", "sender", "Ada Lovelace", "hash", DateTimeOffset.UtcNow, null);
        var recipient = User.FromPersistence(Guid.NewGuid(), "recipient@example.com", "recipient", "Recipient", "hash", DateTimeOffset.UtcNow, null);
        await userRepository.AddAsync(sender, CancellationToken.None);
        await userRepository.AddAsync(recipient, CancellationToken.None);
        var subscriptionRepository = new InMemoryPushSubscriptionRepository();
        await subscriptionRepository.AddOrReplaceAsync(
            PushSubscription.CreateForBrowser(recipient.Id, new WebPushRegistration("https://push.example/a", "p256dh", "auth")), CancellationToken.None);
        var pushSender = new RecordingPushNotificationSender();
        var entryRepository = new InMemoryNotificationEntryRepository();
        var handler = new SendMessageCommandHandler(
            userRepository, new InMemoryChatMessageRepository(), new InMemoryContactRepository(), new InMemoryChatConversationAccessRepository(),
            new PushNotificationDispatcher(subscriptionRepository, [pushSender], NullLogger<PushNotificationDispatcher>.Instance),
            new NotificationRecorder(new InMemoryNotificationSettingsRepository(), entryRepository));

        var result = await handler.HandleAsync(
            new SendMessageCommand(sender.Id, recipient.Id, "ciphertext", "nonce", IsShareInvitation: true), CancellationToken.None);

        // The share this message carries an Accept for has already told the recipient about itself, by
        // name. A "New message" on top of it would be a second entry for one invitation, and the less
        // informative of the two.
        Assert.Equal(SendMessageOutcome.Success, result.Outcome);
        Assert.Empty(pushSender.SentNotifications);
        Assert.Empty(await entryRepository.GetRecentAsync(recipient.Id, 10, CancellationToken.None));
    }

    private static PushNotificationDispatcher CreateDispatcher()
        => new(new InMemoryPushSubscriptionRepository(), [new RecordingPushNotificationSender()], NullLogger<PushNotificationDispatcher>.Instance);

    private static NotificationRecorder CreateNotificationRecorder()
        => new(new InMemoryNotificationSettingsRepository(), new InMemoryNotificationEntryRepository());
}
