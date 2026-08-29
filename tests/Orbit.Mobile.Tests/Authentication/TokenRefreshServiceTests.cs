using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Contracts.Users;
using Orbit.Mobile.Authentication;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Authentication;

/// <summary>
/// The server rotates refresh tokens and revokes the redeemed one atomically, so two refreshes of the
/// same token cannot both win. Orbit.Web shipped that race and it logged people out mid-use; these pin
/// the fix down here, where a burst of concurrent requests is if anything more likely - a screen coming
/// back into the foreground loads everything at once.
/// </summary>
public sealed class TokenRefreshServiceTests
{
    private static readonly UserSession StoredSession = new(
        "expired-access-token", "the-refresh-token", Guid.NewGuid(), "user@orbit.example", "A User");

    [Fact]
    public async Task A_burst_of_callers_redeems_the_refresh_token_exactly_once()
    {
        var serverMayAnswer = new TaskCompletionSource();
        var context = new RefreshContext(StubHttpMessageHandler.Custom(async (_, _) =>
        {
            await serverMayAnswer.Task;
            return Answer(new AuthResponse("new-access", "new-refresh", StoredSession.UserId, StoredSession.Email, StoredSession.DisplayName));
        }));

        var callers = Enumerable.Range(0, 8).Select(_ => context.Service.TryRefreshAsync()).ToArray();
        serverMayAnswer.SetResult();
        var results = await Task.WhenAll(callers);

        // One redemption, and every caller told it succeeded - not one winner and seven sign-outs.
        Assert.Single(context.Handler.ReceivedRequests);
        Assert.All(results, Assert.True);
    }

    [Fact]
    public async Task A_later_refresh_starts_a_new_attempt_rather_than_reusing_the_finished_one()
    {
        var context = new RefreshContext(StubHttpMessageHandler.Custom((_, _) => Task.FromResult(
            Answer(new AuthResponse("new-access", "new-refresh", StoredSession.UserId, StoredSession.Email, StoredSession.DisplayName)))));

        Assert.True(await context.Service.TryRefreshAsync());
        Assert.True(await context.Service.TryRefreshAsync());

        Assert.Equal(2, context.Handler.ReceivedRequests.Count);
    }

    [Fact]
    public async Task A_successful_refresh_stores_both_new_tokens()
    {
        var context = new RefreshContext(StubHttpMessageHandler.RespondingWith(
            new AuthResponse("new-access", "new-refresh", StoredSession.UserId, StoredSession.Email, StoredSession.DisplayName)));

        await context.Service.TryRefreshAsync();

        var stored = Assert.IsType<UserSession>(context.Storage.Stored);
        Assert.Equal("new-access", stored.AccessToken);
        // The old refresh token is dead the moment it is redeemed, so failing to keep the new one would
        // strand the app on a token the server has already revoked.
        Assert.Equal("new-refresh", stored.RefreshToken);
    }

    [Fact]
    public async Task A_rejected_refresh_token_signs_the_user_out()
    {
        var context = new RefreshContext(StubHttpMessageHandler.RespondingWith(HttpStatusCode.Unauthorized));

        Assert.False(await context.Service.TryRefreshAsync());
        Assert.Null(context.Storage.Stored);
    }

    [Fact]
    public async Task With_nobody_signed_in_there_is_nothing_to_ask_the_server()
    {
        var context = new RefreshContext(StubHttpMessageHandler.RespondingWith(HttpStatusCode.Unauthorized), signedIn: false);

        Assert.False(await context.Service.TryRefreshAsync());
        Assert.Empty(context.Handler.ReceivedRequests);
    }

    [Fact]
    public async Task One_caller_giving_up_does_not_cancel_the_refresh_the_others_are_waiting_on()
    {
        var serverMayAnswer = new TaskCompletionSource();
        var context = new RefreshContext(StubHttpMessageHandler.Custom(async (_, _) =>
        {
            await serverMayAnswer.Task;
            return Answer(new AuthResponse("new-access", "new-refresh", StoredSession.UserId, StoredSession.Email, StoredSession.DisplayName));
        }));

        using var impatient = new CancellationTokenSource();
        var abandoned = context.Service.TryRefreshAsync(impatient.Token);
        var patient = context.Service.TryRefreshAsync();
        await impatient.CancelAsync();
        serverMayAnswer.SetResult();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => abandoned);
        // Whoever happened to ask first must not be able to sign everyone else out by navigating away.
        Assert.True(await patient);
    }

    private static HttpResponseMessage Answer(AuthResponse response)
        => new(HttpStatusCode.OK) { Content = System.Net.Http.Json.JsonContent.Create(response) };

    private sealed class RefreshContext
    {
        public RefreshContext(StubHttpMessageHandler handler, bool signedIn = true)
        {
            Handler = handler;
            Storage = new InMemorySessionStorage(signedIn ? StoredSession : null);
            Service = new TokenRefreshService(
                new SessionStore(Storage), handler.ToHttpClient(), NullLogger<TokenRefreshService>.Instance);
        }

        public StubHttpMessageHandler Handler { get; }
        public InMemorySessionStorage Storage { get; }
        public TokenRefreshService Service { get; }
    }
}
