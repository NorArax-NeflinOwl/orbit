using Orbit.Api.Auth;
using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Users.RegisterUser;
using Xunit;

namespace Orbit.Api.Tests.Users;

public sealed class RegisterUserCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_creates_a_user_with_a_hashed_password()
    {
        var repository = new InMemoryUserRepository();
        var handler = new RegisterUserCommandHandler(repository, new PasswordHasher());

        var result = await handler.HandleAsync(
            new RegisterUserCommand("New@Example.com", "New User", "s3cret-password"), CancellationToken.None);

        Assert.NotNull(result.User);
        Assert.Null(result.Error);
        // Registration normalizes the email so login is case-insensitive.
        Assert.Equal("new@example.com", result.User!.Email);
        Assert.NotEqual("s3cret-password", result.User.PasswordHash);

        var stored = await repository.GetByEmailAsync("new@example.com", CancellationToken.None);
        Assert.Equal(result.User.Id, stored!.Id);
    }

    [Fact]
    public async Task HandleAsync_rejects_an_email_that_is_already_registered()
    {
        var repository = new InMemoryUserRepository();
        var passwordHasher = new PasswordHasher();
        var handler = new RegisterUserCommandHandler(repository, passwordHasher);
        await handler.HandleAsync(new RegisterUserCommand("taken@example.com", "First", "password-one"), CancellationToken.None);

        var result = await handler.HandleAsync(
            new RegisterUserCommand("taken@example.com", "Second", "password-two"), CancellationToken.None);

        Assert.Null(result.User);
        Assert.NotNull(result.Error);
    }
}
