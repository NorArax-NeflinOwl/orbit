using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orbit.Contracts.Notes;
using Orbit.Core.Sync;
using Orbit.Mobile.Api;
using Orbit.Mobile.Data;

namespace Orbit.Mobile.Sync;

/// <summary>
/// What one synchronisation run did, for logging and for the screen to show.
/// </summary>
/// <param name="ReachedTheServer">
/// False when the phone never got through at all. Distinct from a run that reached the server and found
/// nothing to do, which looks identical in every other field - the screen needs to tell "up to date"
/// from "cannot tell".
/// </param>
public sealed record SyncResult(int Sent, int Received, int RemovedLocally, int GivenUp, bool ReachedTheServer)
{
    public static SyncResult NeverGotThrough(int givenUp) => new(0, 0, 0, givenUp, ReachedTheServer: false);
}

/// <summary>
/// Brings the phone and the server back into step: send what was done offline, then take what changed
/// elsewhere (info/orbit-maui-plan.md §5.3-5.4).
///
/// The order is deliberate. Pushing first means a note edited on the phone is on the server before the
/// server's view of it comes back, so the pull confirms the local change rather than reverting it.
/// Pulling first would make every offline edit look stale for the length of one round trip.
///
/// Queued changes replay strictly in order and stop at the first failure that might succeed later.
/// Reordering would be worse than waiting: a create that slipped behind its own update turns into an
/// update to a note that does not exist.
/// </summary>
public sealed class NoteSynchronizer
{
    /// <summary>
    /// After this many failures a queued change is dropped. Something the server refuses in a way that
    /// looks retryable - a persistent 500 on one malformed row - would otherwise block every change
    /// queued behind it forever, which costs far more than the one change being abandoned.
    /// </summary>
    private const int MaximumFailedAttempts = 5;

    private readonly IDbContextFactory<OrbitLocalDbContext> _dbContextFactory;
    private readonly NotesClient _notesClient;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<NoteSynchronizer> _logger;

    /// <summary>
    /// One context is created per run and disposed at the end of it - long enough for change tracking
    /// to hold the run together, short enough not to outlive it.
    /// </summary>
    public NoteSynchronizer(
        IDbContextFactory<OrbitLocalDbContext> dbContextFactory, NotesClient notesClient,
        TimeProvider timeProvider, ILogger<NoteSynchronizer> logger)
    {
        _dbContextFactory = dbContextFactory;
        _notesClient = notesClient;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>
    /// Never throws for being offline. Synchronising is something the app does on a timer, on resume,
    /// and on a pull-to-refresh, and on a phone "there is no network" is an ordinary state rather than
    /// an error - making every caller wrap this in a try/catch would guarantee one of them forgets.
    /// </summary>
    public async Task<SyncResult> SynchroniseAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var push = await ReplayOutboxAsync(dbContext, cancellationToken);

        try
        {
            var pull = await PullChangesAsync(dbContext, cancellationToken);
            return new SyncResult(push.Sent, pull.Received, pull.RemovedLocally, push.GivenUp, ReachedTheServer: true);
        }
        catch (Exception exception) when (IsWorthRetrying(exception, cancellationToken))
        {
            _logger.LogInformation("Could not reach the server to pull changes ({Reason})", exception.Message);
            return push.Sent > 0
                ? new SyncResult(push.Sent, 0, 0, push.GivenUp, ReachedTheServer: true)
                : SyncResult.NeverGotThrough(push.GivenUp);
        }
    }

    private async Task<(int Sent, int GivenUp)> ReplayOutboxAsync(OrbitLocalDbContext dbContext, CancellationToken cancellationToken)
    {
        var queued = await dbContext.Outbox.OrderBy(entry => entry.Id).ToListAsync(cancellationToken);
        var sent = 0;
        var givenUp = 0;

        foreach (var entry in queued)
        {
            SendResult result;
            try
            {
                result = await SendAsync(dbContext, entry, cancellationToken);
            }
            catch (Exception exception) when (IsWorthRetrying(exception, cancellationToken))
            {
                // Offline again, or the server faltered. Stop here and keep this change and everything
                // queued behind it - sending the rest out of order is worse than sending none.
                givenUp += await RecordFailureAsync(dbContext, entry, cancellationToken);
                return (sent, givenUp);
            }

            if (result is SendResult.Sent)
            {
                sent++;
            }
            else
            {
                givenUp++;
            }

            dbContext.Outbox.Remove(entry);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return (sent, givenUp);
    }

    /// <summary>
    /// Whether this failure is one that trying again later could fix - no network at all, a timeout, or
    /// a server having a bad moment.
    ///
    /// <see cref="HttpRequestException"/> covers both "there is no network" and "the server answered,
    /// with a status I did not want", and swallowing the second as though it were the first tells a user
    /// whose session has expired that they are offline: wrong, and nothing they can act on. A 401 has to
    /// surface so the app can send them back to sign in.
    /// </summary>
    private static bool IsWorthRetrying(Exception exception, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        return exception switch
        {
            // No response at all - the usual shape of being offline.
            HttpRequestException { StatusCode: null } => true,
            HttpRequestException { StatusCode: { } status } =>
                (int)status >= 500
                || status is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests,
            TaskCanceledException => true,
            _ => false
        };
    }

    /// <summary>Returns 1 when the change was given up on rather than kept for another attempt.</summary>
    private async Task<int> RecordFailureAsync(OrbitLocalDbContext dbContext, OutboxEntry entry, CancellationToken cancellationToken)
    {
        entry.FailedAttempts++;
        var givenUp = 0;

        if (entry.FailedAttempts >= MaximumFailedAttempts)
        {
            _logger.LogWarning(
                "Giving up on a queued {Operation} for note {NoteLocalId} after {Attempts} attempts",
                entry.Operation, entry.NoteLocalId, entry.FailedAttempts);
            dbContext.Outbox.Remove(entry);
            givenUp = 1;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return givenUp;
    }

    private enum SendResult
    {
        Sent,

        /// <summary>The server will never accept it. Dropped, and the next pull restores its version.</summary>
        Abandoned
    }

    private async Task<SendResult> SendAsync(OrbitLocalDbContext dbContext, OutboxEntry entry, CancellationToken cancellationToken)
    {
        if (entry.Operation is OutboxOperation.Delete)
        {
            if (entry.NoteServerId is not { } serverId)
            {
                return SendResult.Abandoned;
            }

            await _notesClient.DeleteAsync(serverId, cancellationToken);
            return SendResult.Sent;
        }

        var note = await dbContext.Notes.FirstOrDefaultAsync(
            candidate => candidate.LocalId == entry.NoteLocalId, cancellationToken);

        if (note is null)
        {
            // Deleted locally before this ever went out. LocalNoteRepository drops the queue for a note
            // the server never saw, so reaching here means the row went away another way.
            return SendResult.Abandoned;
        }

        return entry.Operation is OutboxOperation.Create
            ? await SendCreateAsync(note, cancellationToken)
            : await SendUpdateAsync(note, cancellationToken);
    }

    private async Task<SendResult> SendCreateAsync(LocalNote note, CancellationToken cancellationToken)
    {
        if (note.ServerId is not null)
        {
            // Already created - a duplicate create would make a second note out of one.
            return SendResult.Abandoned;
        }

        note.ServerId = await _notesClient.CreateAsync(
            new CreateNoteRequest(note.Title, note.Content, note.IsPrivate), cancellationToken);
        note.LastSyncedAtUtc = _timeProvider.GetUtcNow();
        return SendResult.Sent;
    }

    private async Task<SendResult> SendUpdateAsync(LocalNote note, CancellationToken cancellationToken)
    {
        if (note.ServerId is not { } serverId)
        {
            // Its create is still queued ahead of this and has not succeeded yet.
            return SendResult.Abandoned;
        }

        var outcome = await _notesClient.UpdateAsync(
            serverId, new UpdateNoteRequest(note.Title, note.Content, note.IsPrivate), cancellationToken);

        if (outcome is not NoteWriteOutcome.Applied)
        {
            _logger.LogInformation("The server refused an offline edit of note {ServerId}: {Outcome}", serverId, outcome);
            return SendResult.Abandoned;
        }

        note.LastSyncedAtUtc = _timeProvider.GetUtcNow();
        return SendResult.Sent;
    }

    private async Task<(int Received, int RemovedLocally)> PullChangesAsync(OrbitLocalDbContext dbContext, CancellationToken cancellationToken)
    {
        var cursor = await dbContext.SyncCursors.FirstOrDefaultAsync(
            entry => entry.EntityType == SyncEntityType.Note, cancellationToken);

        var feed = await _notesClient.GetChangesAsync(cursor?.Value, cancellationToken);

        // A note with changes still queued is the one thing the server's version must not overwrite -
        // the user's unsent work would disappear with no trace that it ever existed.
        var stillQueued = await dbContext.Outbox
            .Select(entry => entry.NoteLocalId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var received = await ApplyChangedAsync(dbContext, feed.Changed, stillQueued, cancellationToken);
        var removed = await ApplyDeletionsAsync(dbContext, feed.DeletedIds, stillQueued, cancellationToken);

        if (cursor is null)
        {
            dbContext.SyncCursors.Add(new SyncCursor { EntityType = SyncEntityType.Note, Value = feed.Cursor });
        }
        else
        {
            cursor.Value = feed.Cursor;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return (received, removed);
    }

    private async Task<int> ApplyChangedAsync(
        OrbitLocalDbContext dbContext, IReadOnlyList<NoteDto> changed, IReadOnlyCollection<Guid> stillQueued,
        CancellationToken cancellationToken)
    {
        var received = 0;
        foreach (var incoming in changed)
        {
            var existing = await dbContext.Notes.FirstOrDefaultAsync(
                note => note.ServerId == incoming.Id, cancellationToken);

            if (existing is not null && stillQueued.Contains(existing.LocalId))
            {
                continue;
            }

            var note = existing ?? NewLocalNote(dbContext, incoming.Id);
            CopyInto(note, incoming);
            received++;
        }

        return received;
    }

    private async Task<int> ApplyDeletionsAsync(
        OrbitLocalDbContext dbContext, IReadOnlyList<Guid> deletedIds, IReadOnlyCollection<Guid> stillQueued,
        CancellationToken cancellationToken)
    {
        var removed = 0;
        foreach (var deletedId in deletedIds)
        {
            var note = await dbContext.Notes.FirstOrDefaultAsync(
                candidate => candidate.ServerId == deletedId, cancellationToken);

            if (note is null || stillQueued.Contains(note.LocalId))
            {
                continue;
            }

            dbContext.Notes.Remove(note);
            removed++;
        }

        return removed;
    }

    private static LocalNote NewLocalNote(OrbitLocalDbContext dbContext, Guid serverId)
    {
        var note = new LocalNote { LocalId = Guid.NewGuid(), ServerId = serverId };
        dbContext.Notes.Add(note);
        return note;
    }

    private void CopyInto(LocalNote note, NoteDto incoming)
    {
        note.Title = incoming.Title;
        note.Content = incoming.Content;
        note.IsPrivate = incoming.IsPrivate;
        note.EncryptedCiphertext = incoming.EncryptedContent?.Ciphertext;
        note.EncryptedNonce = incoming.EncryptedContent?.Nonce;
        note.CreatedAtUtc = incoming.CreatedAtUtc;
        note.UpdatedAtUtc = incoming.UpdatedAtUtc;
        note.IsShared = incoming.IsShared;
        note.SharedByUserName = incoming.SharedByUserName;
        note.IsSharedWithOthers = incoming.IsSharedWithOthers;
        note.AccessLevel = incoming.AccessLevel;
        note.LastSyncedAtUtc = _timeProvider.GetUtcNow();
    }
}
