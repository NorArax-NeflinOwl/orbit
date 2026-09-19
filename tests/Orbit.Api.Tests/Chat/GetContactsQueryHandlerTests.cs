using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Chat;
using Orbit.Core.Chat.GetContacts;
using Orbit.Core.Users;
using Xunit;

namespace Orbit.Api.Tests.Chat;

public sealed class GetContactsQueryHandlerTests
{
    [Fact]
    public async Task HandleAsync_returns_the_other_partys_current_profile()
    {
        var userRepository = new InMemoryUserRepository();
        var otherUser = User.FromPersistence(Guid.NewGuid(), "other@example.com", "other", "Other", "hash", DateTimeOffset.UtcNow, "public-key");
        await userRepository.AddAsync(otherUser, CancellationToken.None);
        var contactRepository = new InMemoryContactRepository();
        var ownerId = Guid.NewGuid();
        var lastMessageAtUtc = DateTimeOffset.UtcNow;
        await contactRepository.EnsureContactAsync(ownerId, otherUser.Id, lastMessageAtUtc, CancellationToken.None);
        var handler = new GetContactsQueryHandler(
            contactRepository, userRepository, new InMemoryChatConversationAccessRepository(), new InMemoryChatMessageRepository());

        var contacts = await handler.HandleAsync(new GetContactsQuery(ownerId), CancellationToken.None);

        var contact = Assert.Single(contacts);
        Assert.Equal(otherUser.DisplayName, contact.User.DisplayName);
        Assert.Equal("public-key", contact.User.PublicKeyBase64);
        Assert.Equal(lastMessageAtUtc, contact.LastMessageAtUtc);
        Assert.False(contact.RequiresApprovalFromCurrentUser);
        Assert.False(contact.IsPendingApprovalFromOtherParty);
    }

    /// <summary>
    /// The row says when the last message was actually sent, not when the Contact row was last bumped.
    /// The two agree until somebody empties the conversation: LastMessageAtUtc is moved forward on a
    /// send and never back, so a reader who cleared their history saw "3 days ago" beside a conversation
    /// showing nothing at all. Asked for 2026-09-19.
    /// </summary>
    [Fact]
    public async Task HandleAsync_reads_the_last_message_rather_than_the_contact_row()
    {
        var userRepository = new InMemoryUserRepository();
        var otherUser = User.FromPersistence(
            Guid.NewGuid(), "other@example.com", "other", "Other", "hash", DateTimeOffset.UtcNow, "public-key");
        await userRepository.AddAsync(otherUser, CancellationToken.None);
        var contactRepository = new InMemoryContactRepository();
        var ownerId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        // The row was bumped this minute; the message it was bumped for went a week ago.
        await contactRepository.EnsureContactAsync(ownerId, otherUser.Id, now, CancellationToken.None);
        var messages = new InMemoryChatMessageRepository();
        await messages.AddAsync(
            ChatMessage.FromPersistence(
                Guid.NewGuid(), otherUser.Id, ownerId, "cipher", "nonce", now.AddDays(-7),
                isEdited: false, editedAtUtc: null),
            CancellationToken.None);
        var handler = new GetContactsQueryHandler(
            contactRepository, userRepository, new InMemoryChatConversationAccessRepository(), messages);

        var contacts = await handler.HandleAsync(new GetContactsQuery(ownerId), CancellationToken.None);

        Assert.Equal(now.AddDays(-7), Assert.Single(contacts).LastMessageAtUtc);
    }

    /// <summary>
    /// And a conversation this reader has emptied answers with the row again: everything before their
    /// own cleared line is gone from their side, so the message behind it is not an answer about what
    /// they can see. The line is this reader's alone - the other party's list is unaffected.
    /// </summary>
    [Fact]
    public async Task HandleAsync_ignores_a_message_from_before_the_reader_emptied_the_conversation()
    {
        var userRepository = new InMemoryUserRepository();
        var otherUser = User.FromPersistence(
            Guid.NewGuid(), "other@example.com", "other", "Other", "hash", DateTimeOffset.UtcNow, "public-key");
        await userRepository.AddAsync(otherUser, CancellationToken.None);
        var contactRepository = new InMemoryContactRepository();
        var ownerId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        await contactRepository.EnsureContactAsync(ownerId, otherUser.Id, now, CancellationToken.None);
        var messages = new InMemoryChatMessageRepository();
        await messages.AddAsync(
            ChatMessage.FromPersistence(
                Guid.NewGuid(), otherUser.Id, ownerId, "cipher", "nonce", now.AddDays(-7),
                isEdited: false, editedAtUtc: null),
            CancellationToken.None);
        await contactRepository.ClearHistoryAsync(ownerId, otherUser.Id, now.AddDays(-1), CancellationToken.None);
        var handler = new GetContactsQueryHandler(
            contactRepository, userRepository, new InMemoryChatConversationAccessRepository(), messages);

        var contacts = await handler.HandleAsync(new GetContactsQuery(ownerId), CancellationToken.None);

        Assert.Equal(now, Assert.Single(contacts).LastMessageAtUtc);
    }

    [Fact]
    public async Task HandleAsync_returns_an_empty_list_for_a_user_with_no_contacts()
    {
        var handler = new GetContactsQueryHandler(
            new InMemoryContactRepository(), new InMemoryUserRepository(), new InMemoryChatConversationAccessRepository(),
            new InMemoryChatMessageRepository());

        var contacts = await handler.HandleAsync(new GetContactsQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Empty(contacts);
    }

    [Fact]
    public async Task HandleAsync_flags_a_contact_who_started_the_conversation_and_is_awaiting_approval()
    {
        var userRepository = new InMemoryUserRepository();
        var otherUser = User.FromPersistence(Guid.NewGuid(), "other@example.com", "other", "Other", "hash", DateTimeOffset.UtcNow, null);
        await userRepository.AddAsync(otherUser, CancellationToken.None);
        var contactRepository = new InMemoryContactRepository();
        var ownerId = Guid.NewGuid();
        await contactRepository.EnsureContactAsync(ownerId, otherUser.Id, DateTimeOffset.UtcNow, CancellationToken.None);
        var conversationAccessRepository = new InMemoryChatConversationAccessRepository();
        await conversationAccessRepository.EnsureCreatedAsync(otherUser.Id, ownerId, CancellationToken.None);
        var handler = new GetContactsQueryHandler(contactRepository, userRepository, conversationAccessRepository, new InMemoryChatMessageRepository());

        var contacts = await handler.HandleAsync(new GetContactsQuery(ownerId), CancellationToken.None);

        var contact = Assert.Single(contacts);
        Assert.True(contact.RequiresApprovalFromCurrentUser);
        Assert.False(contact.IsPendingApprovalFromOtherParty);
    }

    [Fact]
    public async Task HandleAsync_flags_a_contact_the_current_user_started_as_pending_approval_from_the_other_party()
    {
        var userRepository = new InMemoryUserRepository();
        var otherUser = User.FromPersistence(Guid.NewGuid(), "other@example.com", "other", "Other", "hash", DateTimeOffset.UtcNow, null);
        await userRepository.AddAsync(otherUser, CancellationToken.None);
        var contactRepository = new InMemoryContactRepository();
        var ownerId = Guid.NewGuid();
        await contactRepository.EnsureContactAsync(ownerId, otherUser.Id, DateTimeOffset.UtcNow, CancellationToken.None);
        var conversationAccessRepository = new InMemoryChatConversationAccessRepository();
        await conversationAccessRepository.EnsureCreatedAsync(ownerId, otherUser.Id, CancellationToken.None);
        var handler = new GetContactsQueryHandler(contactRepository, userRepository, conversationAccessRepository, new InMemoryChatMessageRepository());

        var contacts = await handler.HandleAsync(new GetContactsQuery(ownerId), CancellationToken.None);

        var contact = Assert.Single(contacts);
        Assert.False(contact.RequiresApprovalFromCurrentUser);
        Assert.True(contact.IsPendingApprovalFromOtherParty);
    }
    [Fact]
    public async Task A_contact_carries_how_many_of_their_messages_are_still_unread()
    {
        var contactRepository = new InMemoryContactRepository();
        var userRepository = new InMemoryUserRepository();
        var messageRepository = new InMemoryChatMessageRepository();
        var reader = User.Create("reader@example.com", "reader", "Reader", "hash");
        var writer = User.Create("writer@example.com", "writer", "Writer", "hash");
        await userRepository.AddAsync(reader, CancellationToken.None);
        await userRepository.AddAsync(writer, CancellationToken.None);
        await contactRepository.EnsureContactAsync(reader.Id, writer.Id, DateTimeOffset.UtcNow, CancellationToken.None);
        await messageRepository.AddAsync(ChatMessage.Create(writer.Id, reader.Id, "a", "n"), CancellationToken.None);
        await messageRepository.AddAsync(ChatMessage.Create(writer.Id, reader.Id, "b", "n"), CancellationToken.None);
        // The reader's own message is not something waiting for them.
        await messageRepository.AddAsync(ChatMessage.Create(reader.Id, writer.Id, "c", "n"), CancellationToken.None);

        var handler = new GetContactsQueryHandler(
            contactRepository, userRepository, new InMemoryChatConversationAccessRepository(), messageRepository);
        var contacts = await handler.HandleAsync(new GetContactsQuery(reader.Id), CancellationToken.None);

        Assert.Equal(2, Assert.Single(contacts).UnreadCount);
    }

    [Fact]
    public async Task Reading_a_conversation_empties_its_unread_count()
    {
        var contactRepository = new InMemoryContactRepository();
        var userRepository = new InMemoryUserRepository();
        var messageRepository = new InMemoryChatMessageRepository();
        var reader = User.Create("reader@example.com", "reader", "Reader", "hash");
        var writer = User.Create("writer@example.com", "writer", "Writer", "hash");
        await userRepository.AddAsync(reader, CancellationToken.None);
        await userRepository.AddAsync(writer, CancellationToken.None);
        await contactRepository.EnsureContactAsync(reader.Id, writer.Id, DateTimeOffset.UtcNow, CancellationToken.None);
        await messageRepository.AddAsync(ChatMessage.Create(writer.Id, reader.Id, "a", "n"), CancellationToken.None);

        await messageRepository.MarkConversationAsReadAsync(
            reader.Id, writer.Id, DateTimeOffset.UtcNow, readUpToUtc: null, CancellationToken.None);

        var handler = new GetContactsQueryHandler(
            contactRepository, userRepository, new InMemoryChatConversationAccessRepository(), messageRepository);
        var contacts = await handler.HandleAsync(new GetContactsQuery(reader.Id), CancellationToken.None);

        Assert.Equal(0, Assert.Single(contacts).UnreadCount);
    }
}