using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Users;
using Orbit.Core.Users.GetWrappedPrivateKey;
using Xunit;

namespace Orbit.Api.Tests.Users;

public sealed class GetWrappedPrivateKeyQueryHandlerTests
{
    [Fact]
    public async Task HandleAsync_returns_the_wrapped_private_key_backed_up_for_the_requesting_user()
    {
        var repository = new InMemoryUserRepository();
        var wrappedPrivateKey = new WrappedPrivateKey("ciphertext", "nonce", "salt", 600000);
        var user = User.FromPersistence(
            Guid.NewGuid(), "owner@example.com", "owner", "Owner", "hash", DateTimeOffset.UtcNow, "base64-key", wrappedPrivateKey);
        await repository.AddAsync(user, CancellationToken.None);
        var handler = new GetWrappedPrivateKeyQueryHandler(repository);

        var result = await handler.HandleAsync(new GetWrappedPrivateKeyQuery(user.Id), CancellationToken.None);

        Assert.Equal(wrappedPrivateKey, result);
    }

    [Fact]
    public async Task HandleAsync_returns_null_for_a_user_who_has_never_backed_up_a_private_key()
    {
        var repository = new InMemoryUserRepository();
        var user = User.FromPersistence(Guid.NewGuid(), "owner@example.com", "owner", "Owner", "hash", DateTimeOffset.UtcNow, null);
        await repository.AddAsync(user, CancellationToken.None);
        var handler = new GetWrappedPrivateKeyQueryHandler(repository);

        var result = await handler.HandleAsync(new GetWrappedPrivateKeyQuery(user.Id), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task HandleAsync_returns_null_for_an_unknown_user()
    {
        var handler = new GetWrappedPrivateKeyQueryHandler(new InMemoryUserRepository());

        var result = await handler.HandleAsync(new GetWrappedPrivateKeyQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Null(result);
    }
}
