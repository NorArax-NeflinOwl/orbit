using Orbit.Api.Auth;
using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Users;
using Orbit.Core.Users.Login;
using Xunit;

namespace Orbit.Api.Tests.Users;

public sealed class LoginQueryHandlerTests
{
    private readonly PasswordHasher _passwordHasher = new();
    private readonly InMemoryUserRepository _repository = new();

    private LoginQueryHandler Handler => new(_repository, _passwordHasher);

    /// <summary>A password of null makes the Google-signed-in kind of account, which has none at all.</summary>
    private async Task<User> AddUserAsync(string? password = "correct-password")
    {
        var user = password is null
            ? User.CreateFromGoogle("login@example.com", "loginuser", "Login User", "google-subject-id")
            : User.Create("login@example.com", "loginuser", "Login User", _passwordHasher.Hash(password));
        await _repository.AddAsync(user, CancellationToken.None);
        return user;
    }

    [Fact]
    public async Task HandleAsync_returns_the_user_for_correct_credentials_via_email()
    {
        var user = await AddUserAsync();

        var result = await Handler.HandleAsync(new LoginQuery("login@example.com", "correct-password"), CancellationToken.None);

        Assert.Equal(LoginRejection.None, result.Rejection);
        Assert.Equal(user.Id, result.User!.Id);
    }

    [Fact]
    public async Task HandleAsync_returns_the_user_for_correct_credentials_via_login()
    {
        var user = await AddUserAsync();

        var result = await Handler.HandleAsync(new LoginQuery("loginuser", "correct-password"), CancellationToken.None);

        Assert.Equal(LoginRejection.None, result.Rejection);
        Assert.Equal(user.Id, result.User!.Id);
    }

    [Fact]
    public async Task HandleAsync_says_the_password_is_wrong_when_the_account_is_there()
    {
        await AddUserAsync();

        var result = await Handler.HandleAsync(new LoginQuery("login@example.com", "wrong-password"), CancellationToken.None);

        // Told apart from an unknown account on purpose - see the handler's comment for what that costs.
        Assert.Null(result.User);
        Assert.Equal(LoginRejection.WrongPassword, result.Rejection);
    }

    [Fact]
    public async Task HandleAsync_says_there_is_no_such_account_for_an_unknown_email_or_login()
    {
        var result = await Handler.HandleAsync(new LoginQuery("nobody@example.com", "whatever"), CancellationToken.None);

        Assert.Null(result.User);
        Assert.Equal(LoginRejection.NoSuchAccount, result.Rejection);
    }

    [Fact]
    public async Task HandleAsync_says_so_when_the_account_has_no_password_at_all()
    {
        await AddUserAsync(password: null);

        var result = await Handler.HandleAsync(new LoginQuery("login@example.com", "whatever"), CancellationToken.None);

        // Reporting a wrong password would send somebody looking for one that does not exist.
        Assert.Null(result.User);
        Assert.Equal(LoginRejection.NoPasswordSet, result.Rejection);
    }
}
