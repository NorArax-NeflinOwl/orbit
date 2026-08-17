using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Users;
using Orbit.Core.Users.SetPublicKey;
using Xunit;

namespace Orbit.Api.Tests.Users;

public sealed class SetPublicKeyCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_stores_the_public_key_on_the_requesting_user()
    {
        var repository = new InMemoryUserRepository();
        var user = User.FromPersistence(Guid.NewGuid(), "owner@example.com", "owner", "Owner", "hash", DateTimeOffset.UtcNow, null);
        await repository.AddAsync(user, CancellationToken.None);
        var handler = new SetPublicKeyCommandHandler(repository);

        var succeeded = await handler.HandleAsync(new SetPublicKeyCommand(user.Id, "base64-key"), CancellationToken.None);

        Assert.True(succeeded);
        Assert.Equal("base64-key", user.PublicKeyBase64);
    }

    [Fact]
    public async Task HandleAsync_returns_false_for_an_unknown_user()
    {
        var handler = new SetPublicKeyCommandHandler(new InMemoryUserRepository());

        var succeeded = await handler.HandleAsync(new SetPublicKeyCommand(Guid.NewGuid(), "base64-key"), CancellationToken.None);

        Assert.False(succeeded);
    }
}
