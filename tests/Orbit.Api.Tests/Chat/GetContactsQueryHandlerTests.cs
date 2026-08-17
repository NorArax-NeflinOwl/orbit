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
        var handler = new GetContactsQueryHandler(contactRepository, userRepository);

        var contacts = await handler.HandleAsync(new GetContactsQuery(ownerId), CancellationToken.None);

        var contact = Assert.Single(contacts);
        Assert.Equal(otherUser.DisplayName, contact.User.DisplayName);
        Assert.Equal("public-key", contact.User.PublicKeyBase64);
        Assert.Equal(lastMessageAtUtc, contact.LastMessageAtUtc);
    }

    [Fact]
    public async Task HandleAsync_returns_an_empty_list_for_a_user_with_no_contacts()
    {
        var handler = new GetContactsQueryHandler(new InMemoryContactRepository(), new InMemoryUserRepository());

        var contacts = await handler.HandleAsync(new GetContactsQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Empty(contacts);
    }
}
