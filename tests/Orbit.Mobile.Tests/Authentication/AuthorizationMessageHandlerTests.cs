using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Contracts.Users;
using Orbit.Mobile.Authentication;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Authentication;

/// <summary>
/// Access tokens last fifteen minutes, so an app open longer than that hits 401 on perfectly ordinary
/// use. The handler turning that into a refresh-and-retry is what stops the user seeing a failure for
/// something that only needed a new token - and what stops every screen writing that logic itself.
/// </summary>
public sealed class AuthorizationMessageHandlerTests
{
    private static readonly UserSession StoredSession = new(
        "expired-access-token", "the-refresh-token", Guid.NewGuid(), "user@orbit.example", "A User");

    [Fact]
    public async Task Every_request_carries_the_access_token()
    {
        var context = new HandlerContext(alwaysAnswering: HttpStatusCode.OK);

        await context.Client.GetAsync("api/notes");

        Assert.Equal("Bearer expired-access-token", context.Api.ReceivedRequests.Single().Authorization);
    }

    [Fact]
    public async Task An_unauthenticated_app_sends_no_authorization_header_at_all()
    {
        var context = new HandlerContext(alwaysAnswering: HttpStatusCode.OK, signedIn: false);

        await context.Client.GetAsync("api/notes");

        Assert.Null(context.Api.ReceivedRequests.Single().Authorization);
    }

    [Fact]
    public async Task An_expired_access_token_is_refreshed_and_the_request_sent_again()
    {
        var context = new HandlerContext(unauthorizedUntilRefreshed: true);

        var response = await context.Client.GetAsync("api/notes");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, context.Api.ReceivedRequests.Count);
        // The retry must carry the *new* token; resending the expired one would 401 again forever.
        Assert.Equal("Bearer refreshed-access-token", context.Api.ReceivedRequests[1].Authorization);
    }

    [Fact]
    public async Task The_retry_sends_the_same_body_as_the_original_request()
    {
        var context = new HandlerContext(unauthorizedUntilRefreshed: true);

        await context.Client.PostAsJsonAsync("api/notes", new { title = "Groceries" });

        // A request that has already been sent cannot be reused, so a handler that forgot to carry the
        // body would silently turn a create into an empty one.
        Assert.Contains("Groceries", context.Api.ReceivedRequests[1].Body);
        Assert.Equal("application/json", context.Api.ReceivedRequests[1].ContentType);
    }

    [Fact]
    public async Task A_refusal_that_is_not_about_the_token_is_returned_untouched()
    {
        var context = new HandlerContext(alwaysAnswering: HttpStatusCode.Forbidden);

        var response = await context.Client.GetAsync("api/notes");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Single(context.Api.ReceivedRequests);
        Assert.Empty(context.Refresh.ReceivedRequests);
    }

    [Fact]
    public async Task When_the_refresh_token_is_dead_too_the_original_refusal_is_what_the_caller_sees()
    {
        var context = new HandlerContext(unauthorizedUntilRefreshed: true, refreshSucceeds: false);

        var response = await context.Client.GetAsync("api/notes");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        // One attempt, not a loop: the refresh failed, so sending it again could only fail identically.
        Assert.Single(context.Api.ReceivedRequests);
    }

    private sealed class HandlerContext
    {
        public HandlerContext(
            HttpStatusCode? alwaysAnswering = null, bool unauthorizedUntilRefreshed = false,
            bool refreshSucceeds = true, bool signedIn = true)
        {
            var refreshed = false;
            Api = StubHttpMessageHandler.Custom((_, _) => Task.FromResult(new HttpResponseMessage(
                alwaysAnswering ?? (unauthorizedUntilRefreshed && !refreshed
                    ? HttpStatusCode.Unauthorized
                    : HttpStatusCode.OK))));

            Refresh = refreshSucceeds
                ? StubHttpMessageHandler.Custom((_, _) =>
                {
                    refreshed = true;
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = JsonContent.Create(new AuthResponse(
                            "refreshed-access-token", "refreshed-refresh-token",
                            StoredSession.UserId, StoredSession.Email, StoredSession.DisplayName))
                    });
                })
                : StubHttpMessageHandler.RespondingWith(HttpStatusCode.Unauthorized);

            var sessionStore = new SessionStore(new InMemorySessionStorage(signedIn ? StoredSession : null));
            var authorization = new AuthorizationMessageHandler(
                sessionStore,
                new TokenRefreshService(sessionStore, Refresh.ToHttpClient(), NullLogger<TokenRefreshService>.Instance))
            {
                InnerHandler = Api
            };

            Client = new HttpClient(authorization) { BaseAddress = new Uri("https://orbit.example/") };
        }

        public StubHttpMessageHandler Api { get; }
        public StubHttpMessageHandler Refresh { get; }
        public HttpClient Client { get; }
    }
}
