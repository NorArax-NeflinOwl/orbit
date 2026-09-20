using Orbit.Api.Auth;
using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Users;
using Orbit.Core.Users.SetPrivatePin;
using Orbit.Core.Users.VerifyPrivatePin;
using Xunit;

namespace Orbit.Api.Tests.Users;

/// <summary>
/// The PIN a client asks for before it shows what is private (asked for 2026-09-20). It is a door
/// rather than a lock: what keeps a private note unreadable is the key it is sealed with, which never
/// reaches the server. This is the question asked before what has already been unsealed is put on a
/// screen somebody else might be standing at.
/// </summary>
public sealed class PrivatePinTests
{
    private const string Password = "correct horse";

    private readonly PasswordHasher _hasher = new();
    private readonly InMemoryUserRepository _users = new();

    private async Task<User> AnAccountAsync()
    {
        var user = User.Create("a@example.com", "anna", "Anna", _hasher.Hash(Password));
        await _users.AddAsync(user, CancellationToken.None);
        return user;
    }

    private SetPrivatePinCommandHandler Setting => new(_users, _hasher);

    private VerifyPrivatePinQueryHandler Checking => new(_users, _hasher);

    [Fact]
    public async Task An_account_has_no_pin_until_it_sets_one()
    {
        var user = await AnAccountAsync();

        Assert.False((await _users.GetByIdAsync(user.Id, CancellationToken.None))!.HasPrivatePin);
    }

    [Fact]
    public async Task Setting_one_needs_the_account_password()
    {
        var user = await AnAccountAsync();

        var set = await Setting.HandleAsync(
            new SetPrivatePinCommand(user.Id, "not the password", "1234"), CancellationToken.None);

        Assert.False(set);
        Assert.False((await _users.GetByIdAsync(user.Id, CancellationToken.None))!.HasPrivatePin);
    }

    /// <summary>
    /// The password rather than the PIN being replaced, deliberately: four digits are a thing people
    /// forget, and a PIN only its rememberer can change is a PIN that locks its own owner out.
    /// </summary>
    [Fact]
    public async Task Changing_one_needs_the_password_and_not_the_old_pin()
    {
        var user = await AnAccountAsync();
        await Setting.HandleAsync(new SetPrivatePinCommand(user.Id, Password, "1234"), CancellationToken.None);

        var changed = await Setting.HandleAsync(
            new SetPrivatePinCommand(user.Id, Password, "9876"), CancellationToken.None);

        Assert.True(changed);
        Assert.True(await Checking.HandleAsync(new VerifyPrivatePinQuery(user.Id, "9876"), CancellationToken.None));
        Assert.False(await Checking.HandleAsync(new VerifyPrivatePinQuery(user.Id, "1234"), CancellationToken.None));
    }

    [Fact]
    public async Task It_is_stored_hashed_rather_than_as_it_was_typed()
    {
        var user = await AnAccountAsync();

        await Setting.HandleAsync(new SetPrivatePinCommand(user.Id, Password, "1234"), CancellationToken.None);

        var stored = (await _users.GetByIdAsync(user.Id, CancellationToken.None))!;
        Assert.NotNull(stored.PrivatePinHash);
        Assert.NotEqual("1234", stored.PrivatePinHash);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("123456789")]
    [InlineData("12a4")]
    [InlineData("")]
    public async Task A_pin_is_four_to_eight_digits_and_nothing_else(string offered)
    {
        var user = await AnAccountAsync();

        var set = await Setting.HandleAsync(
            new SetPrivatePinCommand(user.Id, Password, offered), CancellationToken.None);

        Assert.False(set);
        Assert.False((await _users.GetByIdAsync(user.Id, CancellationToken.None))!.HasPrivatePin);
    }

    /// <summary>
    /// Taking it away is a real answer. Somebody who set one and no longer wants to be asked should not
    /// have to keep answering a question they have withdrawn.
    /// </summary>
    [Fact]
    public async Task It_can_be_taken_away()
    {
        var user = await AnAccountAsync();
        await Setting.HandleAsync(new SetPrivatePinCommand(user.Id, Password, "1234"), CancellationToken.None);

        var taken = await Setting.HandleAsync(
            new SetPrivatePinCommand(user.Id, Password, null), CancellationToken.None);

        Assert.True(taken);
        Assert.False((await _users.GetByIdAsync(user.Id, CancellationToken.None))!.HasPrivatePin);
    }

    /// <summary>
    /// And an account with no PIN answers yes to any of them: there is no door there, and a client that
    /// asks anyway should be let through rather than left holding a question nobody can answer.
    /// </summary>
    [Fact]
    public async Task An_account_without_one_lets_anything_through()
    {
        var user = await AnAccountAsync();

        Assert.True(await Checking.HandleAsync(new VerifyPrivatePinQuery(user.Id, "0000"), CancellationToken.None));
    }

    [Fact]
    public async Task A_wrong_pin_is_refused()
    {
        var user = await AnAccountAsync();
        await Setting.HandleAsync(new SetPrivatePinCommand(user.Id, Password, "1234"), CancellationToken.None);

        Assert.False(await Checking.HandleAsync(new VerifyPrivatePinQuery(user.Id, "4321"), CancellationToken.None));
    }

    [Fact]
    public async Task An_account_that_is_gone_answers_no()
    {
        Assert.False(await Checking.HandleAsync(
            new VerifyPrivatePinQuery(Guid.NewGuid(), "1234"), CancellationToken.None));
    }
}
