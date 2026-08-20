using Orbit.Api.Tests.TestDoubles;
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
        var handler = new GetContactsQueryHandler(contactRepository, userRepository, new InMemoryChatConversationAccessRepository());

        var contacts = await handler.HandleAsync(new GetContactsQuery(ownerId), CancellationToken.None);

        var contact = Assert.Single(contacts);
        Assert.Equal(otherUser.DisplayName, contact.User.DisplayName);
        Assert.Equal("public-key", contact.User.PublicKeyBase64);
        Assert.Equal(lastMessageAtUtc, contact.LastMessageAtUtc);
        Assert.False(contact.RequiresApprovalFromCurrentUser);
        Assert.False(contact.IsPendingApprovalFromOtherParty);
    }

    [Fact]
    public async Task HandleAsync_returns_an_empty_list_for_a_user_with_no_contacts()
    {
        var handler = new GetContactsQueryHandler(
            new InMemoryContactRepository(), new InMemoryUserRepository(), new InMemoryChatConversationAccessRepository());

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
        var handler = new GetContactsQueryHandler(contactRepository, userRepository, conversationAccessRepository);

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
        var handler = new GetContactsQueryHandler(contactRepository, userRepository, conversationAccessRepository);

        var contacts = await handler.HandleAsync(new GetContactsQuery(ownerId), CancellationToken.None);

        var contact = Assert.Single(contacts);
        Assert.False(contact.RequiresApprovalFromCurrentUser);
        Assert.True(contact.IsPendingApprovalFromOtherParty);
    }
}
