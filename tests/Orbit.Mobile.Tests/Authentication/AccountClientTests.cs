using System.Net;
using System.Net.Http.Json;
using Orbit.Contracts.Users;
using Orbit.Mobile.Authentication;
using Orbit.Mobile.Sync;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Authentication;

/// <summary>
/// Account operations are the one part of this app that is deliberately not offline-capable. Notes can
/// wait in an outbox because nothing else depends on when they land; an identity cannot. Each of these
/// needs a verdict only the server can give - is this username free, is this the right password - and
/// each changes how the user signs in everywhere else, not only here.
///
/// The failure being designed out is a queued account change: someone told their password had changed
/// while the old one still worked, possibly for days. So these check that offline is refused up front
/// rather than accepted and remembered.
/// </summary>
public sealed class AccountClientTests
{
    [Fact]
    public async Task Registering_offline_is_refused_rather_than_queued()
    {
        var context = new AccountContext(online: false);

        var result = await context.Client.RegisterAsync("someone@orbit.example", "someone", "Someone", "password");

        Assert.Equal(AccountOperationStatus.RequiresConnection, result.Status);
        // Not a single request went out, and nothing was written locally to send later.
        Assert.Empty(context.Handler.ReceivedRequests);
        Assert.Null(context.Storage.Stored);
    }

    [Fact]
    public async Task Registering_stores_the_session_only_after_the_server_accepted_it()
    {
        var context = new AccountContext(online: true, handler: StubHttpMessageHandler.RespondingWith(
            new AuthResponse("access", "refresh", Guid.NewGuid(), "someone@orbit.example", "Someone")));

        var result = await context.Client.RegisterAsync("someone@orbit.example", "someone", "Someone", "password");

        Assert.True(result.Succeeded);
        Assert.Equal("access", context.Storage.Stored!.AccessToken);
    }

    [Fact]
    public async Task A_username_somebody_else_holds_is_refused_with_the_servers_own_wording()
    {
        var context = new AccountContext(online: true, handler: StubHttpMessageHandler.Custom((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Conflict)
            {
                Content = JsonContent.Create(new { message = "This username is already taken." })
            })));

        var result = await context.Client.ChangeUserNameAsync("taken", "Someone");

        // Only the server can know this, which is exactly why the operation cannot happen offline.
        Assert.Equal(AccountOperationStatus.Refused, result.Status);
        Assert.Equal("This username is already taken.", result.Message);
    }

    [Fact]
    public async Task A_refusal_with_no_readable_message_still_says_something_useful()
    {
        var context = new AccountContext(online: true, handler: StubHttpMessageHandler.RespondingWith(HttpStatusCode.Conflict));

        var result = await context.Client.ChangeUserNameAsync("taken", "Someone");

        Assert.Equal(AccountOperationStatus.Refused, result.Status);
        Assert.Equal("That username is already taken.", result.Message);
    }

    [Fact]
    public async Task Changing_a_password_offline_never_reaches_the_wire()
    {
        var context = new AccountContext(online: false);

        var result = await context.Client.ChangePasswordAsync("old", "new");

        Assert.Equal(AccountOperationStatus.RequiresConnection, result.Status);
        Assert.Empty(context.Handler.ReceivedRequests);
    }

    [Fact]
    public async Task A_wrong_current_password_is_the_servers_call_not_the_phones()
    {
        var context = new AccountContext(online: true, handler: StubHttpMessageHandler.RespondingWith(HttpStatusCode.Unauthorized));

        var result = await context.Client.ChangePasswordAsync("wrong", "new");

        Assert.Equal(AccountOperationStatus.Refused, result.Status);
    }

    [Fact]
    public async Task Changing_an_email_address_offline_is_refused()
    {
        var context = new AccountContext(online: false);

        var result = await context.Client.RequestEmailAddressChangeAsync("new@orbit.example");

        Assert.Equal(AccountOperationStatus.RequiresConnection, result.Status);
        Assert.Empty(context.Handler.ReceivedRequests);
    }

    [Fact]
    public async Task Asking_to_change_an_email_address_does_not_change_it_yet()
    {
        var context = new AccountContext(online: true, handler: StubHttpMessageHandler.RespondingWith(HttpStatusCode.NoContent));

        var result = await context.Client.RequestEmailAddressChangeAsync("new@orbit.example");

        // The server mails a code to the new address; only confirming it completes the change, so an
        // address nobody can receive mail at never becomes the one the account is recovered through.
        Assert.True(result.Succeeded);
        Assert.Equal("POST", context.Handler.ReceivedRequests.Single().Method.Method);
        Assert.EndsWith("/email-verification", context.Handler.ReceivedRequests.Single().Uri!.AbsolutePath);
    }

    [Fact]
    public async Task Confirming_an_email_change_offline_is_refused_too()
    {
        var context = new AccountContext(online: false);

        var result = await context.Client.ConfirmEmailAddressAsync("123456");

        Assert.Equal(AccountOperationStatus.RequiresConnection, result.Status);
    }

    [Fact]
    public async Task Signing_in_offline_is_told_apart_from_a_wrong_password()
    {
        var context = new AccountContext(online: false);
        var authentication = new AuthenticationClient(
            context.Handler.ToHttpClient(), context.NetworkStatus, context.SessionStore);

        var result = await authentication.SignInAsync("someone", "password");

        // Telling someone their password is wrong when the phone simply had no signal sends them off to
        // reset a password that was fine.
        Assert.Equal(AccountOperationStatus.RequiresConnection, result.Status);
        Assert.Empty(context.Handler.ReceivedRequests);
    }

    private sealed class AccountContext
    {
        public AccountContext(bool online, StubHttpMessageHandler? handler = null)
        {
            Handler = handler ?? StubHttpMessageHandler.RespondingWith(HttpStatusCode.NoContent);
            NetworkStatus = new FixedNetworkStatus(online);
            Storage = new InMemorySessionStorage();
            SessionStore = new SessionStore(Storage);
            Client = new AccountClient(Handler.ToHttpClient(), NetworkStatus, SessionStore);
        }

        public StubHttpMessageHandler Handler { get; }
        public INetworkStatus NetworkStatus { get; }
        public InMemorySessionStorage Storage { get; }
        public SessionStore SessionStore { get; }
        public AccountClient Client { get; }
    }
}
