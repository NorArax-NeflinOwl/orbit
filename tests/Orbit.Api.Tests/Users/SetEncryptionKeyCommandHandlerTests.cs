using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Users;
using Orbit.Core.Users.SetEncryptionKey;
using Xunit;

namespace Orbit.Api.Tests.Users;

public sealed class SetEncryptionKeyCommandHandlerTests
{
    private static readonly WrappedPrivateKey WrappedPrivateKey = new("ciphertext", "nonce", "salt", 600000);

    [Fact]
    public async Task HandleAsync_stores_the_public_key_and_wrapped_private_key_on_the_requesting_user()
    {
        var repository = new InMemoryUserRepository();
        var user = User.FromPersistence(Guid.NewGuid(), "owner@example.com", "owner", "Owner", "hash", DateTimeOffset.UtcNow, null);
        await repository.AddAsync(user, CancellationToken.None);
        var handler = new SetEncryptionKeyCommandHandler(repository);

        var succeeded = await handler.HandleAsync(
            new SetEncryptionKeyCommand(user.Id, "base64-key", WrappedPrivateKey), CancellationToken.None);

        Assert.True(succeeded);
        Assert.Equal("base64-key", user.PublicKeyBase64);
        Assert.Equal(WrappedPrivateKey, user.WrappedPrivateKey);
    }

    [Fact]
    public async Task HandleAsync_returns_false_for_an_unknown_user()
    {
        var handler = new SetEncryptionKeyCommandHandler(new InMemoryUserRepository());

        var succeeded = await handler.HandleAsync(
            new SetEncryptionKeyCommand(Guid.NewGuid(), "base64-key", WrappedPrivateKey), CancellationToken.None);

        Assert.False(succeeded);
    }
}
