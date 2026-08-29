using Microsoft.EntityFrameworkCore;
using Orbit.Contracts.Notes;
using Orbit.Mobile.Data;
using Xunit;

namespace Orbit.Mobile.Tests.Sync;

/// <summary>
/// The sync spine, which info/orbit-maui-plan.md §11 names as the largest risk in the whole project: it
/// touches every screen and has no equivalent elsewhere in this codebase to copy from. These are the
/// tests that make it safe to build four more features on top of it, so they cover the awkward paths -
/// losing the network mid-replay, a note deleted on one side and edited on the other - rather than only
/// the happy one.
/// </summary>
public sealed class NoteSynchronizerTests
{
    private static readonly IReadOnlyList<NoteContentLineDto> SomeContent =
        [new NoteContentLineDto("Milk", false, false)];

    [Fact]
    public async Task A_note_written_offline_reaches_the_server_when_the_connection_returns()
    {
        // The headline promise of the whole phase.
        using var context = new SyncContext();
        context.GoOffline();
        await context.Notes.CreateAsync("Groceries", SomeContent);

        context.ComeBackOnline();
        var result = await context.SynchroniseAsync();

        Assert.Equal(1, result.Sent);
        Assert.Contains(context.Server.Notes, note => note.Title == "Groceries");
    }

    [Fact]
    public async Task The_note_keeps_one_identity_across_the_boundary()
    {
        using var context = new SyncContext();
        var created = await context.Notes.CreateAsync("Groceries", SomeContent);

        await context.SynchroniseAsync();

        var stored = await context.DbContext.Notes.SingleAsync();
        // Same local row, now carrying the id the server gave it - not a second copy pulled back down.
        Assert.Equal(created.LocalId, stored.LocalId);
        Assert.NotNull(stored.ServerId);
    }

    [Fact]
    public async Task Editing_offline_sends_an_edit_rather_than_a_second_note()
    {
        using var context = new SyncContext();
        var note = await context.Notes.CreateAsync("Groceries", SomeContent);
        await context.SynchroniseAsync();

        context.GoOffline();
        await context.Notes.UpdateAsync(note.LocalId, "Groceries and bread", SomeContent);
        context.ComeBackOnline();
        await context.SynchroniseAsync();

        Assert.Single(context.Server.Notes);
        Assert.Equal("Groceries and bread", context.Server.Notes.Single().Title);
    }

    [Fact]
    public async Task A_note_created_and_then_edited_offline_arrives_in_the_order_it_happened()
    {
        using var context = new SyncContext();
        context.GoOffline();
        var note = await context.Notes.CreateAsync("Draft", SomeContent);
        await context.Notes.UpdateAsync(note.LocalId, "Finished", SomeContent);

        context.ComeBackOnline();
        await context.SynchroniseAsync();

        // Replaying these the other way round is an update to a note that does not exist yet.
        Assert.Equal(["POST /api/notes", "PUT /api/notes/" + context.Server.Notes.Single().Id], context.WriteRequests());
        Assert.Equal("Finished", context.Server.Notes.Single().Title);
    }

    [Fact]
    public async Task A_note_created_and_deleted_before_ever_syncing_is_never_sent_at_all()
    {
        using var context = new SyncContext();
        context.GoOffline();
        var note = await context.Notes.CreateAsync("Mistake", SomeContent);
        await context.Notes.DeleteAsync(note.LocalId);

        context.ComeBackOnline();
        await context.SynchroniseAsync();

        // Creating it just to delete it would be pointless, and briefly shows other devices a note the
        // user already threw away.
        Assert.Empty(context.WriteRequests());
        Assert.Empty(context.Server.Notes);
    }

    [Fact]
    public async Task Deleting_offline_a_note_the_server_knows_removes_it_there_too()
    {
        using var context = new SyncContext();
        var note = await context.Notes.CreateAsync("Groceries", SomeContent);
        await context.SynchroniseAsync();

        context.GoOffline();
        await context.Notes.DeleteAsync(note.LocalId);
        context.ComeBackOnline();
        await context.SynchroniseAsync();

        Assert.Empty(context.Server.Notes);
        Assert.Empty(await context.DbContext.Outbox.ToListAsync());
    }

    [Fact]
    public async Task Losing_the_network_mid_replay_keeps_the_change_and_everything_behind_it()
    {
        using var context = new SyncContext();
        context.GoOffline();
        await context.Notes.CreateAsync("First", SomeContent);
        await context.Notes.CreateAsync("Second", SomeContent);

        var result = await context.SynchroniseAsync();

        Assert.Equal(0, result.Sent);
        Assert.Equal(2, await context.DbContext.Outbox.CountAsync());
        // Being offline is an ordinary state on a phone, not something to throw at every caller.
        Assert.False(result.ReachedTheServer);
    }

    [Fact]
    public async Task A_change_the_server_keeps_refusing_is_eventually_given_up_on()
    {
        using var context = new SyncContext();
        await context.Notes.CreateAsync("Doomed", SomeContent);
        context.GoOffline();

        // Five runs that each fail; the sixth finds nothing left to block the queue.
        for (var attempt = 0; attempt < 5; attempt++)
        {
            await context.SynchroniseAsync();
        }

        // One permanently failing change must not hold up everything queued behind it forever.
        Assert.Empty(await context.DbContext.Outbox.ToListAsync());
    }

    [Fact]
    public async Task A_note_written_on_the_web_appears_on_the_phone()
    {
        using var context = new SyncContext();
        context.Server.AddNote("Written elsewhere");

        var result = await context.SynchroniseAsync();

        Assert.Equal(1, result.Received);
        Assert.Equal("Written elsewhere", (await context.DbContext.Notes.SingleAsync()).Title);
    }

    [Fact]
    public async Task A_note_deleted_on_the_web_leaves_the_phone_too()
    {
        using var context = new SyncContext();
        var remote = context.Server.AddNote("Doomed");
        await context.SynchroniseAsync();

        context.Clock.Advance(TimeSpan.FromMinutes(1));
        context.Server.DeleteNote(remote.Id);
        var result = await context.SynchroniseAsync();

        // A delta pull cannot see a deletion by absence, which is what the server's tombstones are for.
        Assert.Equal(1, result.RemovedLocally);
        Assert.Empty(await context.DbContext.Notes.ToListAsync());
    }

    [Fact]
    public async Task An_unsent_local_edit_is_not_overwritten_by_the_servers_older_version()
    {
        using var context = new SyncContext();
        var note = await context.Notes.CreateAsync("Groceries", SomeContent);
        await context.SynchroniseAsync();

        context.GoOffline();
        await context.Notes.UpdateAsync(note.LocalId, "Edited on the phone", SomeContent);

        // The network comes back only for the pull - the send still fails.
        context.Server.ForcedFailure = System.Net.HttpStatusCode.InternalServerError;
        context.ComeBackOnline();
        await context.SynchroniseAsync();
        context.Server.ForcedFailure = null;
        await context.SynchroniseAsync();

        // Losing work the user can still see queued would be the worst failure this layer has.
        Assert.Equal("Edited on the phone", (await context.DbContext.Notes.SingleAsync()).Title);
    }

    [Fact]
    public async Task Sending_happens_before_receiving()
    {
        using var context = new SyncContext();
        context.GoOffline();
        await context.Notes.CreateAsync("Groceries", SomeContent);
        context.ComeBackOnline();

        await context.SynchroniseAsync();

        // Pulling first would bring back the server's view of a note the phone has already changed,
        // making every offline edit look stale for the length of one round trip.
        var requests = context.Server.ReceivedRequests;
        Assert.True(
            requests.FindIndex(request => request.StartsWith("POST", StringComparison.Ordinal))
            < requests.FindIndex(request => request.Contains("/changes")));
    }

    [Fact]
    public async Task A_later_pull_stops_asking_for_notes_that_have_not_changed()
    {
        using var context = new SyncContext();
        context.Server.AddNote("Already known");
        context.Clock.Advance(TimeSpan.FromMinutes(1));
        await context.SynchroniseAsync();

        context.Clock.Advance(TimeSpan.FromMinutes(1));
        var result = await context.SynchroniseAsync();

        // The cursor survived the first run, so an unchanged note is not sent down the wire again.
        Assert.Equal(0, result.Received);
        Assert.Single(await context.DbContext.Notes.ToListAsync());
    }

    [Fact]
    public async Task A_note_written_at_the_very_moment_of_a_pull_is_re_sent_rather_than_missed()
    {
        using var context = new SyncContext();
        context.Server.AddNote("Written on the boundary");

        // The cursor is inclusive by design: a change landing while the pull is in flight arrives twice
        // rather than never. Applying the same note twice is harmless - losing it is not.
        await context.SynchroniseAsync();
        var second = await context.SynchroniseAsync();

        Assert.Equal(1, second.Received);
        Assert.Single(await context.DbContext.Notes.ToListAsync());
    }

    [Fact]
    public async Task An_expired_session_is_not_reported_as_being_offline()
    {
        using var context = new SyncContext();
        context.Server.ForcedFailure = System.Net.HttpStatusCode.Unauthorized;

        // The server was reached and had an opinion. Swallowing it as "offline" would leave someone
        // whose session expired staring at stale notes with nothing about their connection to fix.
        var failure = await Assert.ThrowsAsync<HttpRequestException>(() => context.SynchroniseAsync());

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, failure.StatusCode);
    }

    [Fact]
    public async Task A_server_having_a_bad_moment_is_worth_trying_again_rather_than_surfacing()
    {
        using var context = new SyncContext();
        await context.Notes.CreateAsync("Groceries", SomeContent);
        context.Server.ForcedFailure = System.Net.HttpStatusCode.InternalServerError;

        var result = await context.SynchroniseAsync();

        // Unlike a 401, there is nothing for the user to do about a 500 - so the change stays queued and
        // the app tries again rather than throwing at them.
        Assert.False(result.ReachedTheServer);
        Assert.Equal(1, await context.DbContext.Outbox.CountAsync());
    }

    [Fact]
    public async Task Nothing_arrives_twice_when_a_sync_runs_again_with_no_changes()
    {
        using var context = new SyncContext();
        context.Server.AddNote("Steady");
        await context.SynchroniseAsync();
        await context.SynchroniseAsync();

        Assert.Single(await context.DbContext.Notes.ToListAsync());
    }
}
