using System.Net;
using Microsoft.EntityFrameworkCore;
using Orbit.Contracts.Notes;
using Orbit.Mobile.Data;
using Xunit;

namespace Orbit.Mobile.Tests.Sync;

/// <summary>
/// What happens to somebody's work when the outbox cannot get rid of it.
///
/// The rule the app already keeps for chat, applied to the other four: a failure the user cannot see is
/// a failure they cannot act on, and this particular one destroys what they wrote. A log line is not
/// telling anybody.
/// </summary>
public sealed class DroppedWorkIsAnnouncedTests
{
    [Fact]
    public async Task A_change_given_up_on_is_written_into_the_notification_feed()
    {
        using var context = new SyncContext();
        await context.Notes.CreateAsync("Doomed", [new NoteContentLineDto("milk", false, false)]);
        context.Server.ForcedFailure = HttpStatusCode.InternalServerError;

        for (var attempt = 0; attempt < 5; attempt++)
        {
            await context.SynchroniseAsync();
        }

        using var dbContext = context.DbContext;
        var announced = Assert.Single(await dbContext.Notifications.ToListAsync());
        Assert.True(announced.IsRaisedHere);
        Assert.False(announced.IsRead);
        Assert.Contains("note", announced.Body);
    }

    /// <summary>
    /// Nothing was lost, so nothing is said. A note deleted on the phone before its create ever went out
    /// leaves a queue entry with nothing behind it; announcing that would be crying wolf.
    /// </summary>
    [Fact]
    public async Task A_queued_change_with_nothing_behind_it_is_dropped_quietly()
    {
        using var context = new SyncContext();
        var note = await context.Notes.CreateAsync("Written then unwritten", [new NoteContentLineDto("milk", false, false)]);
        await context.Notes.DeleteAsync(note.LocalId);

        await context.SynchroniseAsync();

        using var dbContext = context.DbContext;
        Assert.Empty(await dbContext.Notifications.ToListAsync());
    }

    /// <summary>
    /// And being out of range says nothing either: the change is still queued and still going to be
    /// sent, so there is nothing to report - see SyncFailure.WasAnswered.
    /// </summary>
    [Fact]
    public async Task Being_offline_is_never_announced_as_lost_work()
    {
        using var context = new SyncContext();
        await context.Notes.CreateAsync("Shopping", [new NoteContentLineDto("milk", false, false)]);
        context.GoOffline();

        for (var attempt = 0; attempt < 10; attempt++)
        {
            await context.SynchroniseAsync();
        }

        using var dbContext = context.DbContext;
        Assert.Empty(await dbContext.Notifications.ToListAsync());
        Assert.Equal(1, context.QueuedCount());
    }
}
