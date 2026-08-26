using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orbit.Mobile.Data;

namespace Orbit.Mobile.Sync;

/// <summary>What sending one queued change achieved.</summary>
public enum SendResult
{
    Sent,

    /// <summary>The server will never accept it. Dropped, and the next pull restores its version.</summary>
    Abandoned
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
    /// After this many failures a queued change is dropped. Something the server refuses in a way that
    /// looks retryable - a persistent 500 on one malformed row - would otherwise block every change
    /// behind it forever, which costs far more than the one change being abandoned.
    /// </summary>
    private const int MaximumFailedAttempts = 5;

    public static async Task<ReplayResult> RunAsync(
        OrbitLocalDbContext dbContext, string entityType,
        Func<OutboxEntry, CancellationToken, Task<SendResult>> send,
        ILogger logger, CancellationToken cancellationToken)
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
                givenUp += await RecordFailureAsync(dbContext, entry, logger, cancellationToken);
                return new ReplayResult(sent, givenUp);
            }

            if (result is SendResult.Sent)
            {
                sent++;
            }
            else
            {
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

    /// <summary>Returns 1 when the change was given up on rather than kept for another attempt.</summary>
    private static async Task<int> RecordFailureAsync(
        OrbitLocalDbContext dbContext, OutboxEntry entry, ILogger logger, CancellationToken cancellationToken)
    {
        entry.FailedAttempts++;
        var givenUp = 0;

        if (entry.FailedAttempts >= MaximumFailedAttempts)
        {
            logger.LogWarning(
                "Giving up on a queued {Operation} for {EntityType} {LocalId} after {Attempts} attempts",
                entry.Operation, entry.EntityType, entry.LocalId, entry.FailedAttempts);
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
