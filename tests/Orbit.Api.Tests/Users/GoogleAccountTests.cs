using Orbit.Api.Auth;
using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Users;
using Orbit.Core.Users.LinkGoogleAccount;
using Orbit.Core.Users.Login;
using Orbit.Core.Users.SetPassword;
using Orbit.Core.Users.SignInWithGoogle;
using Xunit;

namespace Orbit.Api.Tests.Users;

/// <summary>
/// Covers what signing in with Google is allowed to do to an account: when it may adopt an existing one,
/// when it must not, and the rules that stop an account ending up with no way back into it.
/// </summary>
public sealed class GoogleAccountTests
{
    [Fact]
    public async Task A_first_sign_in_creates_an_account_with_no_password_and_a_verified_address()
    {
        var context = new GoogleTestContext();

        var user = await context.SignInWithGoogleAsync(StubGoogleIdentityVerifier.ValidToken);

        Assert.NotNull(user);
        Assert.Equal("alice@example.com", user!.Email);
        // Verified because Google only issues a token for an address it confirmed itself - which is what
        // a later password reset relies on.
        Assert.True(user.IsEmailVerified);
        Assert.False(user.HasPassword);
        Assert.Equal("google-subject-1", user.GoogleSubjectId);
    }

    [Fact]
    public async Task Signing_in_again_returns_the_same_account_rather_than_making_a_second_one()
    {
        var context = new GoogleTestContext();
        var first = await context.SignInWithGoogleAsync(StubGoogleIdentityVerifier.ValidToken);

        var second = await context.SignInWithGoogleAsync(StubGoogleIdentityVerifier.ValidToken);

        Assert.Equal(first!.Id, second!.Id);
    }

    [Fact]
    public async Task Signing_in_adopts_an_existing_account_with_the_same_address()
    {
        var context = new GoogleTestContext();
        var existing = await context.AddPasswordUserAsync("alice@example.com", "alice");

        var user = await context.SignInWithGoogleAsync(StubGoogleIdentityVerifier.ValidToken);

        // Safe to link without further proof: holding a Google token for that address *is* proof of
        // controlling the mailbox, the same proof Orbit's own email verification asks for.
        Assert.Equal(existing.Id, user!.Id);
        Assert.Equal("google-subject-1", existing.GoogleSubjectId);
        // The existing password is untouched, so the account keeps both ways in.
        Assert.True(existing.HasPassword);
    }

    [Fact]
    public async Task An_untrustworthy_token_signs_nobody_in_and_creates_nothing()
    {
        var context = new GoogleTestContext();

        var user = await context.SignInWithGoogleAsync("forged-token");

        Assert.Null(user);
        Assert.Null(await context.UserRepository.GetByEmailAsync("alice@example.com", CancellationToken.None));
    }

    [Fact]
    public async Task A_new_account_gets_a_free_username_when_the_obvious_one_is_taken()
    {
        var context = new GoogleTestContext();
        await context.AddPasswordUserAsync("someone-else@example.com", "alice");

        var user = await context.SignInWithGoogleAsync(StubGoogleIdentityVerifier.ValidToken);

        // Derived from the address's local part ("alice"), which someone already has - so it lands on the
        // next free variant rather than colliding.
        Assert.Equal("alice2", user!.UserName);
    }

    [Fact]
    public async Task A_passwordless_account_cannot_be_signed_into_with_any_password()
    {
        var context = new GoogleTestContext();
        await context.SignInWithGoogleAsync(StubGoogleIdentityVerifier.ValidToken);
        var handler = new LoginQueryHandler(context.UserRepository, context.PasswordHasher);

        var loggedIn = await handler.HandleAsync(new LoginQuery("alice@example.com", "anything"), CancellationToken.None);

        // There is no hash to check against, so no password can ever be right - and the empty string is
        // no more special than any other guess.
        Assert.Null(loggedIn);
        Assert.Null(await handler.HandleAsync(new LoginQuery("alice@example.com", ""), CancellationToken.None));
    }

    [Fact]
    public async Task Setting_a_first_password_turns_a_google_account_into_one_that_can_also_sign_in_with_it()
    {
        var context = new GoogleTestContext();
        var user = await context.SignInWithGoogleAsync(StubGoogleIdentityVerifier.ValidToken);
        var setHandler = new SetPasswordCommandHandler(context.UserRepository, context.PasswordHasher);

        var set = await setHandler.HandleAsync(new SetPasswordCommand(user!.Id, "chat-password"), CancellationToken.None);

        Assert.True(set);
        var loggedIn = await new LoginQueryHandler(context.UserRepository, context.PasswordHasher)
            .HandleAsync(new LoginQuery("alice@example.com", "chat-password"), CancellationToken.None);
        Assert.NotNull(loggedIn);
    }

    [Fact]
    public async Task Setting_a_password_is_refused_when_one_already_exists()
    {
        var context = new GoogleTestContext();
        var user = await context.AddPasswordUserAsync("alice@example.com", "alice", password: "original");
        var handler = new SetPasswordCommandHandler(context.UserRepository, context.PasswordHasher);

        var set = await handler.HandleAsync(new SetPasswordCommand(user.Id, "sneaky-overwrite"), CancellationToken.None);

        // Otherwise this endpoint would be a way to change a password without knowing the current one.
        Assert.False(set);
        Assert.True(context.PasswordHasher.Verify("original", user.PasswordHash!));
    }

    [Fact]
    public async Task One_google_identity_cannot_be_linked_to_two_orbit_accounts()
    {
        var context = new GoogleTestContext();
        await context.SignInWithGoogleAsync(StubGoogleIdentityVerifier.ValidToken);
        var other = await context.AddPasswordUserAsync("bob@example.com", "bob");

        var result = await context.LinkGoogleAsync(other.Id, StubGoogleIdentityVerifier.ValidToken);

        Assert.Equal(LinkGoogleAccountResult.AlreadyLinkedElsewhere, result);
        Assert.Null(other.GoogleSubjectId);
    }

    [Fact]
    public async Task Linking_google_to_a_password_account_is_allowed_and_keeps_both_ways_in()
    {
        var context = new GoogleTestContext();
        var user = await context.AddPasswordUserAsync("bob@example.com", "bob");

        var result = await context.LinkGoogleAsync(user.Id, StubGoogleIdentityVerifier.ValidToken);

        Assert.Equal(LinkGoogleAccountResult.Success, result);
        Assert.Equal("google-subject-1", user.GoogleSubjectId);
        Assert.True(user.HasPassword);
    }

    [Fact]
    public async Task Linking_is_refused_when_the_token_is_untrustworthy()
    {
        var context = new GoogleTestContext();
        var user = await context.AddPasswordUserAsync("bob@example.com", "bob");

        var result = await context.LinkGoogleAsync(user.Id, "forged-token");

        Assert.Equal(LinkGoogleAccountResult.InvalidToken, result);
        Assert.Null(user.GoogleSubjectId);
    }

    [Fact]
    public async Task Unlinking_google_is_refused_while_it_is_the_only_way_into_the_account()
    {
        var context = new GoogleTestContext();
        var user = await context.SignInWithGoogleAsync(StubGoogleIdentityVerifier.ValidToken);

        var result = await context.UnlinkGoogleAsync(user!.Id);

        // Allowing this would lock the owner out permanently: no password, and now no Google either.
        Assert.Equal(LinkGoogleAccountResult.WouldLockAccountOut, result);
        Assert.Equal("google-subject-1", user.GoogleSubjectId);
    }

    [Fact]
    public async Task Unlinking_google_is_allowed_once_a_password_exists()
    {
        var context = new GoogleTestContext();
        var user = await context.SignInWithGoogleAsync(StubGoogleIdentityVerifier.ValidToken);
        await new SetPasswordCommandHandler(context.UserRepository, context.PasswordHasher)
            .HandleAsync(new SetPasswordCommand(user!.Id, "chat-password"), CancellationToken.None);

        var result = await context.UnlinkGoogleAsync(user.Id);

        Assert.Equal(LinkGoogleAccountResult.Success, result);
        Assert.Null(user.GoogleSubjectId);
    }

    /// <summary>The collaborator graph these flows need, wired the same way DI wires the real one.</summary>
    private sealed class GoogleTestContext
    {
        public InMemoryUserRepository UserRepository { get; } = new();
        public StubGoogleIdentityVerifier GoogleIdentityVerifier { get; } = new();
        public PasswordHasher PasswordHasher { get; } = new();

        public async Task<User> AddPasswordUserAsync(string email, string userName, string password = "password")
        {
            var user = User.Create(email, userName, userName, PasswordHasher.Hash(password));
            await UserRepository.AddAsync(user, CancellationToken.None);
            return user;
        }

        public Task<User?> SignInWithGoogleAsync(string idToken)
            => new SignInWithGoogleCommandHandler(UserRepository, GoogleIdentityVerifier)
                .HandleAsync(new SignInWithGoogleCommand(idToken), CancellationToken.None);

        public Task<LinkGoogleAccountResult> LinkGoogleAsync(Guid userId, string idToken)
            => new LinkGoogleAccountCommandHandler(UserRepository, GoogleIdentityVerifier)
                .HandleAsync(new LinkGoogleAccountCommand(userId, idToken), CancellationToken.None);

        public Task<LinkGoogleAccountResult> UnlinkGoogleAsync(Guid userId)
            => new UnlinkGoogleAccountCommandHandler(UserRepository)
                .HandleAsync(new UnlinkGoogleAccountCommand(userId), CancellationToken.None);
    }
}
