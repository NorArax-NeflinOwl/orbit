using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Contracts;
using Orbit.Contracts.Users;
using Orbit.Mobile.Authentication;
using Orbit.Mobile.Sync;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Sync;

/// <summary>
/// The address the phone talks to still answers with the server stopped - the environment's front door
/// says 404 - and read as the API's opinion that status signed every phone out within fifteen minutes
/// and had the outbox discarding edits by the evening. The handler is what keeps that status from ever
/// reaching the code that made those decisions.
/// </summary>
public sealed class OrbitAnswerHandlerTests
{
    private static readonly UserSession StoredSession = new(
        "expired-access-token", "the-refresh-token", Guid.NewGuid(), "user@orbit.example", "A User");

    [Fact]
    public async Task An_answer_Orbit_stamped_goes_through_untouched_whatever_its_status()
    {
        var reachability = Reachability.Over(FixedNetworkStatus.Online);
        var client = Through(reachability, StubHttpMessageHandler.Custom((_, _) => Task.FromResult(Stamped(HttpStatusCode.NotFound))));

        var response = await client.GetAsync("api/notes/missing");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task An_answer_nobody_stamped_becomes_a_failure_with_no_status_at_all()
    {
        var reachability = Reachability.Over(FixedNetworkStatus.Online);
        var client = Through(reachability, StubHttpMessageHandler.RespondingWith(HttpStatusCode.NotFound));

        var thrown = await Assert.ThrowsAsync<AnswerNotFromOrbitException>(() => client.GetAsync("api/notes"));

        // The property every rule reads is empty on purpose; the code the platform sent is kept beside it.
        Assert.Null(thrown.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, thrown.AnsweredWith);
    }

    /// <summary>
    /// The two rules the exception's shape exists for. Worth retrying, so the synchroniser treats it as
    /// no signal; not answered, so the outbox does not count it towards giving a change up.
    /// </summary>
    [Fact]
    public void Such_a_failure_is_worth_retrying_and_counts_as_unanswered()
    {
        var failure = new AnswerNotFromOrbitException(HttpStatusCode.NotFound, new Uri("https://orbit.example/api/notes"));

        Assert.True(SyncFailure.IsWorthRetrying(failure, CancellationToken.None));
        Assert.True(SyncFailure.StaysInTheOutbox(failure, CancellationToken.None));
        Assert.False(SyncFailure.WasAnswered(failure));
    }

    /// <summary>
    /// The defect that defeated the offline design. TokenRefreshService signs out on any unsuccessful
    /// refresh, which is right when Orbit refused the token and wrong when the front door answered in
    /// Orbit's place; with the handler beneath it, the refresh never sees the latter as a status.
    /// </summary>
    [Fact]
    public async Task A_refresh_answered_by_something_other_than_Orbit_keeps_the_session()
    {
        var reachability = Reachability.Over(FixedNetworkStatus.Online);
        var storage = new InMemorySessionStorage(StoredSession);
        var sessionStore = new SessionStore(storage);
        var refresh = new TokenRefreshService(
            sessionStore, Through(reachability, StubHttpMessageHandler.RespondingWith(HttpStatusCode.NotFound)),
            NullLogger<TokenRefreshService>.Instance);

        await Assert.ThrowsAsync<AnswerNotFromOrbitException>(() => refresh.TryRefreshAsync());

        Assert.Equal(StoredSession, storage.Stored);
    }

    [Fact]
    public async Task A_refresh_Orbit_itself_refused_still_signs_out()
    {
        var reachability = Reachability.Over(FixedNetworkStatus.Online);
        var storage = new InMemorySessionStorage(StoredSession);
        var sessionStore = new SessionStore(storage);
        var refresh = new TokenRefreshService(
            sessionStore, Through(reachability, StubHttpMessageHandler.Custom((_, _) => Task.FromResult(Stamped(HttpStatusCode.Unauthorized)))),
            NullLogger<TokenRefreshService>.Instance);

        Assert.False(await refresh.TryRefreshAsync());

        Assert.Null(storage.Stored);
    }

    [Fact]
    public async Task The_reachability_is_told_either_way()
    {
        var pauseNotice = new FixedPauseNotice { Notice = new PauseNotice("Back on the 1st.", null) };
        var reachability = Reachability.Over(FixedNetworkStatus.Online, pauseNotice);
        var answers = new Queue<HttpResponseMessage>([new HttpResponseMessage(HttpStatusCode.NotFound), Stamped(HttpStatusCode.OK)]);
        var client = Through(reachability, StubHttpMessageHandler.Custom((_, _) => Task.FromResult(answers.Dequeue())));

        await Assert.ThrowsAsync<AnswerNotFromOrbitException>(() => client.GetAsync("api/notes"));
        Assert.True(reachability.IsPaused);

        await client.GetAsync("api/notes");
        Assert.False(reachability.IsPaused);
    }

    private static HttpResponseMessage Stamped(HttpStatusCode status)
    {
        var response = new HttpResponseMessage(status);
        response.Headers.Add(OrbitAnswerHeader.Name, "0.3.0");
        return response;
    }

    private static HttpClient Through(ServerReachability reachability, HttpMessageHandler server)
        => new(new OrbitAnswerHandler(reachability) { InnerHandler = server }) { BaseAddress = new Uri("https://orbit.example/") };
}
