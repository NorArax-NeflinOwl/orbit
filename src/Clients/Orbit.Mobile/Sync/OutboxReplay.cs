using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orbit.Core.Sync;
using Orbit.Mobile.Data;

namespace Orbit.Mobile.Sync;

/// <summary>What sending one queued change achieved.</summary>
public enum SendResult
{
    Sent,

    /// <summary>
    /// There was nothing to send after all - the row was deleted locally first, or a create had already
    /// succeeded. Dropped silently, because nothing was lost.
    /// </summary>
    Abandoned,

    /// <summary>
    /// The server took the request and would not have it. Dropped like <see cref="Abandoned"/>, and the
    /// next pull restores its version - but somebody's edit went with it, so this one is said out loud.
    /// </summary>
    Refused
}

public sealed record ReplayResult(int Sent, int GivenUp);

/// <summary>
/// Replays one entity type's queued changes, in order, stopping at the first failure another attempt
/// could fix.
///
/// Shared rather than written per feature because the rules are not per feature: order is the order the
/// user did things in, a failure that might pass later must not let the changes behind it overtake it,
/// and a change the server keeps refusing has to be given up on eventually or it blocks the queue
/// forever. What *is* per feature - how a create, an update or a delete is actually sent - is the
/// delegate this takes.
/// </summary>
public static class OutboxReplay
{
    /// <summary>
    /// After this many <i>answered</i> failures a queued change is dropped. Something the server refuses
    /// in a way that looks retryable - a persistent 500 on one malformed row - would otherwise block
    /// every change behind it forever, which costs far more than the one change being abandoned.
    ///
    /// Answered is the whole of it: a phone with no signal has not been refused anything, and counting
    /// that would delete somebody's work for having been out of range five times - see
    /// <see cref="SyncFailure.WasAnswered"/>.
    /// </summary>
    private const int MaximumFailedAttempts = 5;

    public static async Task<ReplayResult> RunAsync(
        OrbitLocalDbContext dbContext, string entityType,
        Func<OutboxEntry, CancellationToken, Task<SendResult>> send,
        TimeProvider timeProvider, ILogger logger, CancellationToken cancellationToken)
    {
        var queued = await dbContext.Outbox
            .Where(entry => entry.EntityType == entityType)
            .OrderBy(entry => entry.Id)
            .ToListAsync(cancellationToken);

        var sent = 0;
        var givenUp = 0;

        foreach (var entry in queued)
        {
            SendResult result;
            try
            {
                result = await send(entry, cancellationToken);
            }
            catch (Exception exception) when (SyncFailure.IsWorthRetrying(exception, cancellationToken))
            {
                // Offline again, or the server faltered. Stop here and keep this change and everything
                // queued behind it - sending the rest out of order is worse than sending none.
                givenUp += await RecordFailureAsync(
                    dbContext, entry, SyncFailure.WasAnswered(exception), timeProvider, logger, cancellationToken);

                return new ReplayResult(sent, givenUp);
            }

            if (result is SendResult.Sent)
            {
                sent++;
            }
            else
            {
                if (result is SendResult.Refused)
                {
                    AnnounceAsDropped(dbContext, entry, timeProvider);
                }

                givenUp++;
            }

            // Whatever the send changed on the row itself - a server id, a synced-at stamp - is saved
            // before the queue entry goes, so a crash in between replays a change that is now a no-op
            // rather than losing one.
            await dbContext.SaveChangesAsync(cancellationToken);
            await RemoveAsync(dbContext, entry.Id, cancellationToken);
        }

        return new ReplayResult(sent, givenUp);
    }

    /// <summary>
    /// Writes down, where the reader will see it, that a change they made has been dropped.
    ///
    /// A log line is not telling anybody. This is somebody's work being thrown away to keep the queue
    /// moving, and it is the one thing here that cannot be undone - so it goes into the feed, in the
    /// same save as the deletion, and stays there until they clear it.
    ///
    /// No destination: there is nothing to open that would help. The change is gone, and a tap landing
    /// on the thing as it now stands would suggest otherwise.
    /// </summary>
    private static void AnnounceAsDropped(
        OrbitLocalDbContext dbContext, OutboxEntry entry, TimeProvider timeProvider)
        => dbContext.Notifications.Add(new LocalNotification
        {
            Id = Guid.NewGuid(),
            Kind = "ChangeDropped",
            Title = "A change couldn't be saved",
            Body = DroppedDescription(entry.EntityType),
            Url = null,
            CreatedAtUtc = timeProvider.GetUtcNow(),
            IsRaisedHere = true
        });

    /// <summary>
    /// One whole sentence per kind rather than a noun dropped into a shared one. Polish declines what
    /// was refused, so "zmiany w notatce" and "…w liście zadań" cannot come from the same template -
    /// the same reason the server writes its share notifications out one by one.
    /// </summary>
    private static string DroppedDescription(string entityType)
        => entityType switch
        {
            SyncEntityType.Note => "Orbit kept refusing a change to a note, so it is no longer waiting to be sent.",
            SyncEntityType.TaskList => "Orbit kept refusing a change to a task list, so it is no longer waiting to be sent.",
            SyncEntityType.CalendarEvent => "Orbit kept refusing a change to an appointment, so it is no longer waiting to be sent.",
            SyncEntityType.Warehouse => "Orbit kept refusing a change to a warehouse, so it is no longer waiting to be sent.",
            _ => "Orbit kept refusing a change, so it is no longer waiting to be sent."
        };

    /// <summary>Returns 1 when the change was given up on rather than kept for another attempt.</summary>
    /// <param name="wasAnswered">
    /// Whether the server answered at all. Only an answer counts against the limit - see
    /// <see cref="MaximumFailedAttempts"/>.
    /// </param>
    private static async Task<int> RecordFailureAsync(
        OrbitLocalDbContext dbContext, OutboxEntry entry, bool wasAnswered, TimeProvider timeProvider,
        ILogger logger, CancellationToken cancellationToken)
    {
        if (!wasAnswered)
        {
            return 0;
        }

        entry.FailedAttempts++;
        var givenUp = 0;

        if (entry.FailedAttempts >= MaximumFailedAttempts)
        {
            logger.LogWarning(
                "Giving up on a queued {Operation} for {EntityType} {LocalId} after {Attempts} attempts",
                entry.Operation, entry.EntityType, entry.LocalId, entry.FailedAttempts);
            AnnounceAsDropped(dbContext, entry, timeProvider);
            givenUp = 1;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        if (givenUp == 1)
        {
            await RemoveAsync(dbContext, entry.Id, cancellationToken);
        }

        return givenUp;
    }

    /// <summary>
    /// Deleted by id rather than through the change tracker, so an entry another run already removed is
    /// simply not there instead of throwing "expected to affect 1 row, affected 0". SyncGate should stop
    /// that happening at all; this makes it harmless when something slips past.
    /// </summary>
    private static Task RemoveAsync(OrbitLocalDbContext dbContext, long entryId, CancellationToken cancellationToken)
        => dbContext.Outbox.Where(entry => entry.Id == entryId).ExecuteDeleteAsync(cancellationToken);
}
