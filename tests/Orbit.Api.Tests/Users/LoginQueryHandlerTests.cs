using Orbit.Api.Auth;
using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Users;
using Orbit.Core.Users.Login;
using Xunit;

namespace Orbit.Api.Tests.Users;

public sealed class LoginQueryHandlerTests
{
    [Fact]
    public async Task HandleAsync_returns_the_user_for_correct_credentials_via_email()
    {
        var passwordHasher = new PasswordHasher();
        var repository = new InMemoryUserRepository();
        var user = User.Create("login@example.com", "loginuser", "Login User", passwordHasher.Hash("correct-password"));
        await repository.AddAsync(user, CancellationToken.None);
        var handler = new LoginQueryHandler(repository, passwordHasher);

        var result = await handler.HandleAsync(new LoginQuery("login@example.com", "correct-password"), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(user.Id, result!.Id);
    }

    [Fact]
    public async Task HandleAsync_returns_the_user_for_correct_credentials_via_username()
    {
        var passwordHasher = new PasswordHasher();
        var repository = new InMemoryUserRepository();
        var user = User.Create("login@example.com", "loginuser", "Login User", passwordHasher.Hash("correct-password"));
        await repository.AddAsync(user, CancellationToken.None);
        var handler = new LoginQueryHandler(repository, passwordHasher);

        var result = await handler.HandleAsync(new LoginQuery("loginuser", "correct-password"), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(user.Id, result!.Id);
    }

    [Fact]
    public async Task HandleAsync_returns_null_for_a_wrong_password()
    {
        var passwordHasher = new PasswordHasher();
        var repository = new InMemoryUserRepository();
        await repository.AddAsync(
            User.Create("login@example.com", "loginuser", "Login User", passwordHasher.Hash("correct-password")), CancellationToken.None);
        var handler = new LoginQueryHandler(repository, passwordHasher);

        var result = await handler.HandleAsync(new LoginQuery("login@example.com", "wrong-password"), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task HandleAsync_returns_null_for_an_unknown_email_or_username()
    {
        var handler = new LoginQueryHandler(new InMemoryUserRepository(), new PasswordHasher());

        var result = await handler.HandleAsync(new LoginQuery("nobody@example.com", "whatever"), CancellationToken.None);

        Assert.Null(result);
    }
}
