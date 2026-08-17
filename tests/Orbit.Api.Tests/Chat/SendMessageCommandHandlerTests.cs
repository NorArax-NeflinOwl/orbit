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
        var handler = new SendMessageCommandHandler(userRepository, messageRepository, contactRepository);
        var senderId = Guid.NewGuid();

        var message = await handler.HandleAsync(
            new SendMessageCommand(senderId, recipient.Id, "ciphertext", "nonce"), CancellationToken.None);

        Assert.NotNull(message);
        Assert.Equal("ciphertext", message!.CiphertextBase64);

        var senderContacts = await contactRepository.GetAllForUserAsync(senderId, CancellationToken.None);
        var recipientContacts = await contactRepository.GetAllForUserAsync(recipient.Id, CancellationToken.None);
        Assert.Equal(recipient.Id, Assert.Single(senderContacts).ContactUserId);
        Assert.Equal(senderId, Assert.Single(recipientContacts).ContactUserId);
    }

    [Fact]
    public async Task HandleAsync_returns_null_when_the_recipient_does_not_exist()
    {
        var handler = new SendMessageCommandHandler(
            new InMemoryUserRepository(), new InMemoryChatMessageRepository(), new InMemoryContactRepository());

        var message = await handler.HandleAsync(
            new SendMessageCommand(Guid.NewGuid(), Guid.NewGuid(), "ciphertext", "nonce"), CancellationToken.None);

        Assert.Null(message);
    }

    [Fact]
    public async Task HandleAsync_reuses_the_existing_contact_on_a_second_message()
    {
        var userRepository = new InMemoryUserRepository();
        var recipient = User.FromPersistence(Guid.NewGuid(), "recipient@example.com", "recipient", "Recipient", "hash", DateTimeOffset.UtcNow, null);
        await userRepository.AddAsync(recipient, CancellationToken.None);
        var contactRepository = new InMemoryContactRepository();
        var handler = new SendMessageCommandHandler(userRepository, new InMemoryChatMessageRepository(), contactRepository);
        var senderId = Guid.NewGuid();

        await handler.HandleAsync(new SendMessageCommand(senderId, recipient.Id, "first", "nonce"), CancellationToken.None);
        await handler.HandleAsync(new SendMessageCommand(senderId, recipient.Id, "second", "nonce"), CancellationToken.None);

        var senderContacts = await contactRepository.GetAllForUserAsync(senderId, CancellationToken.None);
        Assert.Single(senderContacts);
    }
}
