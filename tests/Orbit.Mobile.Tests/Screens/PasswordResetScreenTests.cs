using System.Net;
using Orbit.Mobile.Authentication;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens.Authentication;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Screens;

/// <summary>
/// Getting back into an account whose password has been forgotten.
///
/// The endpoints behind this have existed all along and both clients wrapped them; what was missing was
/// a way in that did not require being signed in already. So the part worth pinning is not the request -
/// it is what the screen says and refuses to say: never whether the account exists, and never a
/// password that was only typed correctly once.
/// </summary>
public sealed class PasswordResetScreenTests
{
    [Fact]
    public async Task Asking_for_a_code_says_the_same_thing_whether_or_not_the_account_exists()
    {
        // The server answers an unknown address exactly as it answers a real one, on purpose. A screen
        // that reported "no such account" would turn the sign-in screen into a way of testing whether
        // somebody has an Orbit account.
        foreach (var answer in new[] { HttpStatusCode.NoContent, HttpStatusCode.NoContent })
        {
            var context = new ResetContext(StubHttpMessageHandler.RespondingWith(answer));
            var screen = context.Open();
            screen.EmailOrUserName = "someone@orbit.example";

            await screen.SendCodeCommand.ExecuteAsync(null);

            Assert.True(screen.CodeWasRequested);
            Assert.Contains("If that account exists", screen.Message);
        }
    }

    [Fact]
    public async Task The_code_and_the_new_password_are_only_asked_for_once_a_code_was_sent()
    {
        var context = new ResetContext();
        var screen = context.Open();

        Assert.False(screen.CodeWasRequested);

        screen.EmailOrUserName = "someone@orbit.example";
        await screen.SendCodeCommand.ExecuteAsync(null);

        Assert.True(screen.CodeWasRequested);
    }

    /// <summary>
    /// The rule the whole second field exists for: there is nothing to check a new password against,
    /// since the old one is forgotten by definition, so a typo would lock the account a second time -
    /// with the code already spent.
    /// </summary>
    [Fact]
    public async Task A_new_password_typed_two_different_ways_is_not_sent_anywhere()
    {
        var context = new ResetContext();
        var screen = context.Open();
        await AskForACodeAsync(screen);

        screen.Code = "123456";
        screen.Password = "a-new-password";
        screen.RepeatedPassword = "a-new-passwrod";
        await screen.SetPasswordCommand.ExecuteAsync(null);

        Assert.Contains("don't match", screen.Message);
        Assert.DoesNotContain(
            context.Handler.ReceivedRequests, request => request.Uri!.AbsolutePath.EndsWith("/confirm"));
    }

    [Fact]
    public async Task A_code_and_a_password_typed_twice_sets_it()
    {
        var context = new ResetContext();
        var screen = context.Open();
        await AskForACodeAsync(screen);

        screen.Code = "123456";
        screen.Password = "a-new-password";
        screen.RepeatedPassword = "a-new-password";
        await screen.SetPasswordCommand.ExecuteAsync(null);

        var confirmation = Assert.Single(
            context.Handler.ReceivedRequests, request => request.Uri!.AbsolutePath.EndsWith("/confirm"));
        Assert.Contains("a-new-password", confirmation.Body);
        Assert.True(screen.IsDone);
    }

    /// <summary>
    /// The form goes away rather than staying open behind a message: the code has been spent, so leaving
    /// the fields there invites a second attempt that can only fail.
    /// </summary>
    [Fact]
    public async Task Once_it_worked_the_form_is_gone_and_the_password_is_not_left_lying_about()
    {
        var context = new ResetContext();
        var screen = context.Open();
        await AskForACodeAsync(screen);

        screen.Code = "123456";
        screen.Password = "a-new-password";
        screen.RepeatedPassword = "a-new-password";
        await screen.SetPasswordCommand.ExecuteAsync(null);

        Assert.False(screen.IsNotDone);
        Assert.Empty(screen.Password);
        Assert.Empty(screen.RepeatedPassword);
        Assert.Empty(screen.Code);
        Assert.Contains("Sign in with the new one", screen.Message);
    }

    [Fact]
    public async Task A_code_that_is_no_longer_valid_says_so_and_leaves_the_form_up()
    {
        var context = new ResetContext(StubHttpMessageHandler.Custom((request, _) =>
            Task.FromResult(new HttpResponseMessage(
                request.RequestUri!.AbsolutePath.EndsWith("/confirm")
                    ? HttpStatusCode.BadRequest
                    : HttpStatusCode.NoContent))));
        var screen = context.Open();
        await AskForACodeAsync(screen);

        screen.Code = "000000";
        screen.Password = "a-new-password";
        screen.RepeatedPassword = "a-new-password";
        await screen.SetPasswordCommand.ExecuteAsync(null);

        Assert.False(screen.IsDone);
        Assert.NotEmpty(screen.Message);
    }

    [Fact]
    public async Task Being_out_of_reach_says_so_rather_than_looking_like_a_refusal()
    {
        var context = new ResetContext(StubHttpMessageHandler.Unreachable());
        var screen = context.Open();
        screen.EmailOrUserName = "someone@orbit.example";

        await screen.SendCodeCommand.ExecuteAsync(null);

        Assert.False(screen.CodeWasRequested);
        Assert.Contains("connection", screen.Message);
    }

    /// <summary>None of this can be queued - see AccountClient - so a phone with no signal says so first.</summary>
    [Fact]
    public void Somebody_offline_is_told_before_they_type_anything()
    {
        var context = new ResetContext(online: false);

        Assert.True(context.Open().IsOffline);
    }

    [Fact]
    public void Nothing_is_asked_for_without_an_account_to_ask_about()
    {
        var screen = new ResetContext().Open();

        Assert.False(screen.SendCodeCommand.CanExecute(null));

        screen.EmailOrUserName = "someone@orbit.example";

        Assert.True(screen.SendCodeCommand.CanExecute(null));
    }

    private static async Task AskForACodeAsync(PasswordResetViewModel screen)
    {
        screen.EmailOrUserName = "someone@orbit.example";
        await screen.SendCodeCommand.ExecuteAsync(null);
    }

    private sealed class ResetContext
    {
        public ResetContext(StubHttpMessageHandler? handler = null, bool online = true)
        {
            Handler = handler ?? StubHttpMessageHandler.RespondingWith(HttpStatusCode.NoContent);
            Client = new AccountClient(
                Handler.ToHttpClient(), new FixedNetworkStatus(online), new SessionStore(new InMemorySessionStorage()));
            IsOnline = online;
        }

        public StubHttpMessageHandler Handler { get; }

        public AccountClient Client { get; }

        private bool IsOnline { get; }

        public RecordingScreenNavigator Navigator { get; } = new();

        public PasswordResetViewModel Open()
            => new(Client, new FixedNetworkStatus(IsOnline), new Translations(new InMemoryLanguageStore()), Navigator);
    }
}
