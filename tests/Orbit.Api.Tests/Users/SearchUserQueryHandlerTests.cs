using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Users;
using Orbit.Core.Users.SearchUser;
using Xunit;

namespace Orbit.Api.Tests.Users;

public sealed class SearchUserQueryHandlerTests
{
    [Fact]
    public async Task HandleAsync_finds_a_user_by_exact_email_address()
    {
        var repository = new InMemoryUserRepository();
        var target = User.FromPersistence(Guid.NewGuid(), "target@example.com", "target", "Target", "hash", DateTimeOffset.UtcNow, null);
        await repository.AddAsync(target, CancellationToken.None);
        var handler = new SearchUserQueryHandler(repository, FindableAccounts.Only(target.Id));

        var result = await handler.HandleAsync(new SearchUserQuery(Guid.NewGuid(), "target@example.com"), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(target.Id, result!.Id);
    }

    [Fact]
    public async Task HandleAsync_finds_a_user_by_exact_username()
    {
        var repository = new InMemoryUserRepository();
        var target = User.FromPersistence(Guid.NewGuid(), "target@example.com", "target", "Target", "hash", DateTimeOffset.UtcNow, null);
        await repository.AddAsync(target, CancellationToken.None);
        var handler = new SearchUserQueryHandler(repository, FindableAccounts.Only(target.Id));

        var result = await handler.HandleAsync(new SearchUserQuery(Guid.NewGuid(), "target"), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(target.Id, result!.Id);
    }

    [Fact]
    public async Task HandleAsync_returns_null_for_a_partial_match()
    {
        var repository = new InMemoryUserRepository();
        var target = User.FromPersistence(Guid.NewGuid(), "target@example.com", "target", "Target", "hash", DateTimeOffset.UtcNow, null);
        await repository.AddAsync(target, CancellationToken.None);
        // Findable, so what this test proves is that a prefix is not a match - not that nobody is there.
        var handler = new SearchUserQueryHandler(repository, FindableAccounts.Only(target.Id));

        var result = await handler.HandleAsync(new SearchUserQuery(Guid.NewGuid(), "targe"), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task HandleAsync_returns_null_when_the_requesting_user_searches_for_themselves()
    {
        var repository = new InMemoryUserRepository();
        var self = User.FromPersistence(Guid.NewGuid(), "self@example.com", "self", "Self", "hash", DateTimeOffset.UtcNow, null);
        await repository.AddAsync(self, CancellationToken.None);
        var handler = new SearchUserQueryHandler(repository, FindableAccounts.Only(self.Id));

        var result = await handler.HandleAsync(new SearchUserQuery(self.Id, "self@example.com"), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task HandleAsync_returns_null_for_an_account_that_cannot_be_found()
    {
        var repository = new InMemoryUserRepository();
        var hidden = User.FromPersistence(Guid.NewGuid(), "hidden@example.com", "hidden", "Hidden", "hash", DateTimeOffset.UtcNow, null);
        await repository.AddAsync(hidden, CancellationToken.None);
        // An account that has not unlocked Contacts is invisible: the same "no such user" a made-up
        // address gets, because "found, but hidden" would be finding them.
        var handler = new SearchUserQueryHandler(repository, FindableAccounts.None());

        var result = await handler.HandleAsync(new SearchUserQuery(Guid.NewGuid(), "hidden@example.com"), CancellationToken.None);

        Assert.Null(result);
    }
}
