using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Orbit.Mobile.Authentication;
using Orbit.Mobile.Chat;
using Orbit.Mobile.Crypto;
using Orbit.Mobile.Data;
using Orbit.Mobile.Sync;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Sync;

/// <summary>
/// What the run over every feature does with a failure one of them let through. Each synchroniser
/// answers "could not reach the server" for itself; this is about the ones that get past them, because
/// this method is called on a timer, on resume and on a pull-to-refresh, and there is no screen behind
/// it to catch anything.
/// </summary>
public sealed class EverythingSynchronizerTests
{
    /// <summary>
    /// Something answered, and it was not this API: a gateway's HTML error page, a captive portal, a
    /// proxy that swallowed the body. That is a `JsonException` rather than an `HttpRequestException`,
    /// and it went straight past the catch here until 2026-09-10 - out of a method every screen calls
    /// without a try of its own. Found by a test double answering a folder create with the wrong shape.
    ///
    /// It reads as unreachable rather than refused: nothing about a body this build cannot parse says
    /// the reader may not have what they asked for, and everything queued stays queued.
    /// </summary>
    [Fact]
    public async Task An_answer_this_build_cannot_read_is_reported_as_not_getting_through()
    {
        using var localStore = new LocalStore();
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-09-10T10:00:00Z"));
        using var somethingElse = StubHttpMessageHandler.Custom((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<html><body>502 Bad Gateway</body></html>")
            }));

        var synchronizer = SynchronizerReadingNotesFrom(localStore, clock, somethingElse.ToHttpClient());

        var result = await synchronizer.SynchroniseAsync();

        Assert.False(result.ReachedTheServer);
    }

    /// <summary>Everything else about that run is unchanged: nothing is sent, nothing is dropped.</summary>
    [Fact]
    public async Task An_answer_this_build_cannot_read_leaves_the_queue_alone()
    {
        using var localStore = new LocalStore();
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-09-10T10:00:00Z"));
        var notes = new LocalNoteRepository(localStore, clock, FixedNetworkStatus.Online, PrivateContent.WithoutAKey());
        await notes.CreateAsync("Written offline", [new Contracts.Notes.NoteContentLineDto("Milk", false, false)]);

        using var somethingElse = StubHttpMessageHandler.Custom((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create("not a note") }));

        var synchronizer = SynchronizerReadingNotesFrom(localStore, clock, somethingElse.ToHttpClient());
        await synchronizer.SynchroniseAsync();

        await using var dbContext = localStore.CreateDbContext();
        Assert.Single(dbContext.Outbox);
    }

    /// <summary>
    /// The run with one feature pointed at a server that answers something else entirely, and every
    /// other feature pointed at nobody - which is what a phone with no signal looks like, and is not
    /// what is being tested here.
    /// </summary>
    private static EverythingSynchronizer SynchronizerReadingNotesFrom(
        LocalStore localStore, TimeProvider clock, HttpClient notes)
        => Synchronizers.Against(
            localStore,
            new ChatRepository(localStore, clock),
            UnlockedPermissions.For(localStore),
            new SessionStore(new InMemorySessionStorage(
                new UserSession("access", "refresh", Guid.NewGuid(), "me@orbit.example", "Ala"))),
            notes,
            StubHttpMessageHandler.Unreachable().ToHttpClient(),
            StubHttpMessageHandler.Unreachable().ToHttpClient(),
            StubHttpMessageHandler.Unreachable().ToHttpClient());
}
