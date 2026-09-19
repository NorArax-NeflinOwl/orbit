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
    /// A conversation the reader has emptied says when its newest <em>visible</em> message was sent, not
    /// when the row was last bumped. LastMessageAtUtc is moved forward on a send and never back, so a
    /// reader who cleared their history at noon and has one message since saw the row still claiming the
    /// time of something they can no longer open. Asked for 2026-09-19.
    /// </summary>
    [Fact]
    public async Task HandleAsync_reads_an_emptied_conversation_from_the_messages_it_still_shows()
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
        // Emptied a day ago; the newest message this reader can still open went two hours ago. The row
        // was bumped for it and for everything before it, which is the part they cannot see.
        await contactRepository.ClearHistoryAsync(ownerId, otherUser.Id, now.AddDays(-1), CancellationToken.None);
        await messages.AddAsync(
            ChatMessage.FromPersistence(
                Guid.NewGuid(), otherUser.Id, ownerId, "cipher", "nonce", now.AddHours(-2),
                isEdited: false, editedAtUtc: null),
            CancellationToken.None);
        var handler = new GetContactsQueryHandler(
            contactRepository, userRepository, new InMemoryChatConversationAccessRepository(), messages);

        var contacts = await handler.HandleAsync(new GetContactsQuery(ownerId), CancellationToken.None);

        Assert.Equal(now.AddHours(-2), Assert.Single(contacts).LastMessageAtUtc);
    }

    /// <summary>
    /// And one emptied with nothing since answers with the row: that is still when the conversation was
    /// last active, which is what orders the list - a client that draws a time decides for itself
    /// whether to draw one at all. The line is this reader's alone; the other party's list is untouched.
    /// </summary>
    [Fact]
    public async Task HandleAsync_keeps_the_rows_answer_for_a_conversation_emptied_with_nothing_since()
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

    /// <summary>
    /// A conversation nobody has emptied is not asked about at all: the row is moved forward on every
    /// send, so it already is the last message's time. Pinned because the saving is the point - this
    /// list is re-read on every poll tick, and aggregating every message a busy account ever exchanged
    /// for an answer that is already correct would be a real cost.
    /// </summary>
    [Fact]
    public async Task HandleAsync_does_not_read_the_messages_for_a_conversation_nobody_emptied()
    {
        var userRepository = new InMemoryUserRepository();
        var otherUser = User.FromPersistence(
            Guid.NewGuid(), "other@example.com", "other", "Other", "hash", DateTimeOffset.UtcNow, "public-key");
        await userRepository.AddAsync(otherUser, CancellationToken.None);
        var contactRepository = new InMemoryContactRepository();
        var ownerId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        await contactRepository.EnsureContactAsync(ownerId, otherUser.Id, now, CancellationToken.None);
        var messages = new CountingChatMessageRepository();
        var handler = new GetContactsQueryHandler(
            contactRepository, userRepository, new InMemoryChatConversationAccessRepository(), messages);

        await handler.HandleAsync(new GetContactsQuery(ownerId), CancellationToken.None);

        Assert.Empty(messages.LastMessageTimesAskedAbout);
    }

    /// <summary>Records which conversations the handler asked the messages about.</summary>
    private sealed class CountingChatMessageRepository : InMemoryChatMessageRepository
    {
        public List<Guid> LastMessageTimesAskedAbout { get; } = [];

        public override Task<IReadOnlyDictionary<Guid, DateTimeOffset>> GetLastMessageTimesAsync(
            Guid readerUserId, IReadOnlyCollection<Guid> otherUserIds, CancellationToken cancellationToken)
        {
            LastMessageTimesAskedAbout.AddRange(otherUserIds);
            return base.GetLastMessageTimesAsync(readerUserId, otherUserIds, cancellationToken);
        }
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