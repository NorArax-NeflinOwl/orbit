using Microsoft.EntityFrameworkCore;

namespace Orbit.Mobile.Data;

/// <summary>
/// Gives a row the server has never seen a second try at being created, when nothing queued would
/// create it any more.
///
/// Such a row exists because <see cref="Orbit.Mobile.Sync.OutboxReplay"/> gives up on a create after
/// five answered refusals: it says so in the feed and drops the queue entry, but the row stays, with no
/// server id. Every later edit used to queue an update, and an update on a row the server has never
/// seen is abandoned quietly at send time - so the row was local-only for good, reading like any other
/// with nothing on it saying so. True of every kind of thing, which is why this is written once.
///
/// Silent on purpose: the edit is the retry. The create reads the row as it stands when it goes out, so
/// whatever the edit changed travels with it, and a server that refused the first attempt for something
/// the reader has since corrected takes the second. A row nobody edits again stays local - the other
/// design, a mark on the row with "send again" under its menu, was not the one chosen (see
/// info/future-plan.md).
///
/// Not for a copy still awaiting review: it has no create queued on purpose, because the review is what
/// sends it. Callers that can reach one check <see cref="CopiesForEditing.IsAwaitingReview"/> first.
/// </summary>
public static class LostCreates
{
    /// <summary>
    /// Queues a create for this row when it has no server id and no create is waiting for it, and says
    /// whether it did - in which case the caller queues nothing else, because the create carries the edit.
    ///
    /// False for a row the server knows, and for one whose create is still queued: the first is edited
    /// as usual, and the second is already on its way.
    /// </summary>
    public static async Task<bool> QueueAgainAsync(
        OrbitLocalDbContext dbContext, string entityType, Guid localId, Guid? serverId,
        DateTimeOffset queuedAtUtc, CancellationToken cancellationToken)
    {
        if (serverId is not null)
        {
            return false;
        }

        var createStillQueued = await dbContext.Outbox.AnyAsync(
            entry => entry.EntityType == entityType
                && entry.LocalId == localId
                && entry.Operation == OutboxOperation.Create,
            cancellationToken);

        if (createStillQueued)
        {
            return false;
        }

        dbContext.Outbox.Add(new OutboxEntry
        {
            EntityType = entityType,
            LocalId = localId,
            Operation = OutboxOperation.Create,
            QueuedAtUtc = queuedAtUtc
        });

        return true;
    }
}
