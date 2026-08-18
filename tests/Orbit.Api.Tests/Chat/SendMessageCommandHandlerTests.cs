using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Chat.SendMessage;
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
            userRepository, messageRepository, contactRepository, new InMemoryChatConversationAccessRepository());
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
            new InMemoryChatConversationAccessRepository());

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
            userRepository, new InMemoryChatMessageRepository(), contactRepository, new InMemoryChatConversationAccessRepository());
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
            userRepository, new InMemoryChatMessageRepository(), new InMemoryContactRepository(), new InMemoryChatConversationAccessRepository());
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
            userRepository, new InMemoryChatMessageRepository(), new InMemoryContactRepository(), new InMemoryChatConversationAccessRepository());

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
            userRepository, new InMemoryChatMessageRepository(), new InMemoryContactRepository(), conversationAccessRepository);

        await handler.HandleAsync(new SendMessageCommand(initiator.Id, recipient.Id, "first", "nonce"), CancellationToken.None);
        await conversationAccessRepository.ApproveAsync(recipient.Id, initiator.Id, CancellationToken.None);
        var result = await handler.HandleAsync(new SendMessageCommand(recipient.Id, initiator.Id, "reply", "nonce"), CancellationToken.None);

        Assert.Equal(SendMessageOutcome.Success, result.Outcome);
    }
}
