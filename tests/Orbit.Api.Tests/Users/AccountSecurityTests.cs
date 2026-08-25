using Orbit.Api.Auth;
using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Users;
using Orbit.Core.Users.ChangePassword;
using Orbit.Core.Users.DeleteAccount;
using Orbit.Core.Users.ConfirmEmailVerification;
using Orbit.Core.Users.RequestEmailVerification;
using Orbit.Core.Users.RequestPasswordReset;
using Orbit.Core.Users.ResetPassword;
using Orbit.Core.Users.UpdateProfile;
using Xunit;

namespace Orbit.Api.Tests.Users;

/// <summary>
/// Covers the rules that make the account flows safe rather than merely functional: an address is only
/// written once proved, a reset only reaches a verified address, codes die after enough wrong guesses,
/// and none of it leaks whether an account exists.
/// </summary>
public sealed class AccountSecurityTests
{
    [Fact]
    public async Task Requesting_a_code_for_a_new_address_does_not_change_the_account_until_it_is_confirmed()
    {
        var context = new AccountTestContext();
        var user = await context.AddUserAsync("old@example.com", "alice");

        await context.RequestEmailVerificationAsync(user.Id, "new@example.com");

        // The point of the whole design: a typo can't take over the account before anyone proves they
        // can read mail at the new address.
        Assert.Equal("old@example.com", user.Email);
        Assert.False(user.IsEmailVerified);
        Assert.Equal("new@example.com", Assert.Single(context.EmailSender.SentEmails).ToEmailAddress);
    }

    [Fact]
    public async Task Confirming_the_code_switches_the_account_to_the_new_address_and_marks_it_verified()
    {
        var context = new AccountTestContext();
        var user = await context.AddUserAsync("old@example.com", "alice");
        await context.RequestEmailVerificationAsync(user.Id, "new@example.com");

        var confirmed = await context.ConfirmEmailVerificationAsync(user.Id, TestVerificationCodeGenerator.FixedCode);

        Assert.True(confirmed);
        Assert.Equal("new@example.com", user.Email);
        Assert.True(user.IsEmailVerified);
    }

    [Fact]
    public async Task An_address_already_used_by_another_account_is_rejected_before_any_email_goes_out()
    {
        var context = new AccountTestContext();
        await context.AddUserAsync("taken@example.com", "bob");
        var user = await context.AddUserAsync("alice@example.com", "alice");

        var result = await context.RequestEmailVerificationAsync(user.Id, "taken@example.com");

        Assert.Equal(EmailVerificationRequestResult.EmailTaken, result);
        Assert.Empty(context.EmailSender.SentEmails);
    }

    [Fact]
    public async Task A_code_stops_working_after_too_many_wrong_guesses()
    {
        var context = new AccountTestContext();
        var user = await context.AddUserAsync("alice@example.com", "alice");
        await context.RequestEmailVerificationAsync(user.Id, "alice@example.com");

        for (var attempt = 0; attempt < UserVerificationCode.MaxFailedAttempts; attempt++)
        {
            Assert.False(await context.ConfirmEmailVerificationAsync(user.Id, "000000"));
        }

        // Even the right code is refused now - this is what keeps a six-digit code from being guessable.
        Assert.False(await context.ConfirmEmailVerificationAsync(user.Id, TestVerificationCodeGenerator.FixedCode));
        Assert.False(user.IsEmailVerified);
    }

    [Fact]
    public async Task Requesting_a_second_code_retires_the_first()
    {
        var context = new AccountTestContext();
        var user = await context.AddUserAsync("alice@example.com", "alice");
        await context.RequestEmailVerificationAsync(user.Id, "first@example.com");
        await context.RequestEmailVerificationAsync(user.Id, "second@example.com");

        var confirmed = await context.ConfirmEmailVerificationAsync(user.Id, TestVerificationCodeGenerator.FixedCode);

        // Both codes read the same in this stub, so what's being pinned is that the *newest* one wins:
        // the account lands on the address from the second request, not the first.
        Assert.True(confirmed);
        Assert.Equal("second@example.com", user.Email);
    }

    [Fact]
    public async Task A_password_reset_is_never_emailed_to_an_unverified_address()
    {
        var context = new AccountTestContext();
        await context.AddUserAsync("alice@example.com", "alice");

        var result = await context.RequestPasswordResetAsync("alice@example.com");

        // Reported as success regardless - see RequestPasswordResetCommand - but nothing is sent.
        Assert.True(result);
        Assert.Empty(context.EmailSender.SentEmails);
    }

    [Fact]
    public async Task A_password_reset_for_an_unknown_account_looks_exactly_like_one_for_a_known_account()
    {
        var context = new AccountTestContext();

        var result = await context.RequestPasswordResetAsync("nobody@example.com");

        Assert.True(result);
        Assert.Empty(context.EmailSender.SentEmails);
    }

    [Fact]
    public async Task A_verified_account_can_reset_its_password_with_the_emailed_code()
    {
        var context = new AccountTestContext();
        var user = await context.AddUserAsync("alice@example.com", "alice");
        await context.VerifyEmailAsync(user);
        await context.RequestPasswordResetAsync("alice@example.com");

        var reset = await context.ResetPasswordAsync("alice@example.com", TestVerificationCodeGenerator.FixedCode, "brand-new-password");

        Assert.True(reset);
        Assert.True(context.PasswordHasher.Verify("brand-new-password", user.PasswordHash!));
    }

    [Fact]
    public async Task Changing_a_password_requires_the_current_one()
    {
        var context = new AccountTestContext();
        var user = await context.AddUserAsync("alice@example.com", "alice", password: "original");
        var handler = new ChangePasswordCommandHandler(context.UserRepository, context.PasswordHasher);

        var wrongCurrent = await handler.HandleAsync(
            new ChangePasswordCommand(user.Id, "not-the-password", "new-password"), CancellationToken.None);
        Assert.False(wrongCurrent);
        Assert.True(context.PasswordHasher.Verify("original", user.PasswordHash!));

        var changed = await handler.HandleAsync(
            new ChangePasswordCommand(user.Id, "original", "new-password"), CancellationToken.None);
        Assert.True(changed);
        Assert.True(context.PasswordHasher.Verify("new-password", user.PasswordHash!));
    }

    [Fact]
    public async Task Deleting_the_account_requires_the_current_password()
    {
        var context = new AccountTestContext();
        var user = await context.AddUserAsync("alice@example.com", "alice", password: "original");
        var handler = new DeleteAccountCommandHandler(context.UserRepository, context.PasswordHasher, context.AccountDeletionRepository);

        var wrongPassword = await handler.HandleAsync(new DeleteAccountCommand(user.Id, "not-the-password"), CancellationToken.None);
        Assert.False(wrongPassword);
        Assert.Empty(context.AccountDeletionRepository.DeletedUserIds);

        var deleted = await handler.HandleAsync(new DeleteAccountCommand(user.Id, "original"), CancellationToken.None);
        Assert.True(deleted);
        Assert.Equal(user.Id, Assert.Single(context.AccountDeletionRepository.DeletedUserIds));
    }

    [Fact]
    public async Task Deleting_an_unknown_account_fails_without_wiping_anything()
    {
        var context = new AccountTestContext();
        var handler = new DeleteAccountCommandHandler(context.UserRepository, context.PasswordHasher, context.AccountDeletionRepository);

        var deleted = await handler.HandleAsync(new DeleteAccountCommand(Guid.NewGuid(), "whatever"), CancellationToken.None);

        Assert.False(deleted);
        Assert.Empty(context.AccountDeletionRepository.DeletedUserIds);
    }

    [Fact]
    public async Task A_username_already_taken_by_someone_else_is_rejected()
    {
        var context = new AccountTestContext();
        await context.AddUserAsync("bob@example.com", "bob");
        var user = await context.AddUserAsync("alice@example.com", "alice");
        var handler = new UpdateProfileCommandHandler(context.UserRepository);

        var result = await handler.HandleAsync(new UpdateProfileCommand(user.Id, "Alice", "bob"), CancellationToken.None);

        Assert.Equal(UpdateProfileResult.UserNameTaken, result);
        Assert.Equal("alice", user.UserName);
    }

    [Fact]
    public async Task Keeping_your_own_username_while_changing_the_display_name_is_allowed()
    {
        var context = new AccountTestContext();
        var user = await context.AddUserAsync("alice@example.com", "alice");
        var handler = new UpdateProfileCommandHandler(context.UserRepository);

        var result = await handler.HandleAsync(new UpdateProfileCommand(user.Id, "Alice Cooper", "alice"), CancellationToken.None);

        Assert.Equal(UpdateProfileResult.Success, result);
        Assert.Equal("Alice Cooper", user.DisplayName);
    }

    /// <summary>The collaborator graph these flows need, wired the same way DI wires the real one.</summary>
    private sealed class AccountTestContext
    {
        public InMemoryUserRepository UserRepository { get; } = new();
        public InMemoryAccountDeletionRepository AccountDeletionRepository { get; } = new();
        public InMemoryUserVerificationCodeRepository CodeRepository { get; } = new();
        public TestVerificationCodeGenerator CodeGenerator { get; } = new();
        public RecordingEmailSender EmailSender { get; } = new();
        public PasswordHasher PasswordHasher { get; } = new();

        public async Task<User> AddUserAsync(string email, string userName, string password = "password")
        {
            var user = User.Create(email, userName, userName, PasswordHasher.Hash(password));
            await UserRepository.AddAsync(user, CancellationToken.None);
            return user;
        }

        public Task<EmailVerificationRequestResult> RequestEmailVerificationAsync(Guid userId, string emailAddress)
            => new RequestEmailVerificationCommandHandler(UserRepository, CodeRepository, CodeGenerator, EmailSender)
                .HandleAsync(new RequestEmailVerificationCommand(userId, emailAddress), CancellationToken.None);

        public Task<bool> ConfirmEmailVerificationAsync(Guid userId, string code)
            => new ConfirmEmailVerificationCommandHandler(UserRepository, CodeRepository, CodeGenerator)
                .HandleAsync(new ConfirmEmailVerificationCommand(userId, code), CancellationToken.None);

        /// <summary>Takes an account through the real verify flow, for the tests that need an already-verified address.</summary>
        public async Task VerifyEmailAsync(User user)
        {
            await RequestEmailVerificationAsync(user.Id, user.Email);
            await ConfirmEmailVerificationAsync(user.Id, TestVerificationCodeGenerator.FixedCode);
            EmailSender.SentEmails.ToList().Clear();
        }

        public Task<bool> RequestPasswordResetAsync(string emailOrUserName)
            => new RequestPasswordResetCommandHandler(UserRepository, CodeRepository, CodeGenerator, EmailSender)
                .HandleAsync(new RequestPasswordResetCommand(emailOrUserName), CancellationToken.None);

        public Task<bool> ResetPasswordAsync(string emailOrUserName, string code, string newPassword)
            => new ResetPasswordCommandHandler(UserRepository, CodeRepository, CodeGenerator, PasswordHasher)
                .HandleAsync(new ResetPasswordCommand(emailOrUserName, code, newPassword), CancellationToken.None);
    }
}
