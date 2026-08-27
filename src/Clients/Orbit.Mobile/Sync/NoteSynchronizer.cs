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
/// Brings notes and the server back into step: send what was done offline, then take what changed
/// elsewhere (info/orbit-maui-plan.md §5.3-5.4).
///
/// The order is deliberate. Pushing first means a note edited on the phone is on the server before the
/// server's view of it comes back, so the pull confirms the local change rather than reverting it.
/// Pulling first would make every offline edit look stale for the length of one round trip.
///
/// The parts that are not about notes - replaying a queue in order, deciding which failures are worth
/// retrying, remembering how far this device has caught up - live in <see cref="OutboxReplay"/>,
/// <see cref="SyncFailure"/> and <see cref="SyncCursors"/>, because task lists need exactly the same
/// rules and copying them would mean four chances to get them subtly different.
/// </summary>
public sealed class NoteSynchronizer
{
    private readonly IDbContextFactory<OrbitLocalDbContext> _dbContextFactory;
    private readonly NotesClient _notesClient;
    private readonly TimeProvider _timeProvider;
    private readonly SyncGate _syncGate;
    private readonly ILogger<NoteSynchronizer> _logger;

    /// <summary>
    /// One context is created per run and disposed at the end of it - long enough for change tracking to
    /// hold the run together, short enough not to outlive it.
    /// </summary>
    public NoteSynchronizer(
        IDbContextFactory<OrbitLocalDbContext> dbContextFactory, NotesClient notesClient,
        TimeProvider timeProvider, SyncGate syncGate, ILogger<NoteSynchronizer> logger)
    {
        _dbContextFactory = dbContextFactory;
        _notesClient = notesClient;
        _timeProvider = timeProvider;
        _syncGate = syncGate;
        _logger = logger;
    }

    /// <summary>
    /// Never throws for being offline. Synchronising is something the app does on a timer, on resume,
    /// and on a pull-to-refresh, and on a phone "there is no network" is an ordinary state rather than
    /// an error - making every caller wrap this in a try/catch would guarantee one of them forgets.
    /// </summary>
    public Task<SyncResult> SynchroniseAsync(CancellationToken cancellationToken = default)
        // Serialised rather than run alongside another - see SyncGate for what overlapping costs.
        => _syncGate.RunAsync(SyncEntityType.Note, () => RunAsync(cancellationToken), cancellationToken);

    private async Task<SyncResult> RunAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var push = await OutboxReplay.RunAsync(
            dbContext, SyncEntityType.Note,
            (entry, token) => SendAsync(dbContext, entry, token), _logger, cancellationToken);

        try
        {
            var pull = await PullChangesAsync(dbContext, cancellationToken);
            return new SyncResult(push.Sent, pull.Received, pull.RemovedLocally, push.GivenUp, ReachedTheServer: true);
        }
        catch (Exception exception) when (SyncFailure.IsWorthRetrying(exception, cancellationToken))
        {
            _logger.LogInformation("Could not reach the server to pull changes ({Reason})", exception.Message);
            return push.Sent > 0
                ? new SyncResult(push.Sent, 0, 0, push.GivenUp, ReachedTheServer: true)
                : SyncResult.NeverGotThrough(push.GivenUp);
        }
    }

    private async Task<SendResult> SendAsync(OrbitLocalDbContext dbContext, OutboxEntry entry, CancellationToken cancellationToken)
    {
        if (entry.Operation is OutboxOperation.Delete)
        {
            if (entry.ServerId is not { } serverId)
            {
                return SendResult.Abandoned;
            }

            await _notesClient.DeleteAsync(serverId, cancellationToken);
            return SendResult.Sent;
        }

        var note = await dbContext.Notes.FirstOrDefaultAsync(
            candidate => candidate.LocalId == entry.LocalId, cancellationToken);

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

        if (outcome is not WriteOutcome.Applied)
        {
            _logger.LogInformation("The server refused an offline edit of note {ServerId}: {Outcome}", serverId, outcome);
            return SendResult.Abandoned;
        }

        note.LastSyncedAtUtc = _timeProvider.GetUtcNow();
        return SendResult.Sent;
    }

    private async Task<(int Received, int RemovedLocally)> PullChangesAsync(
        OrbitLocalDbContext dbContext, CancellationToken cancellationToken)
    {
        var cursor = await SyncCursors.ReadAsync(dbContext, SyncEntityType.Note, cancellationToken);
        var feed = await _notesClient.GetChangesAsync(cursor, cancellationToken);

        // A note with changes still queued is the one thing the server's version must not overwrite -
        // the user's unsent work would disappear with no trace that it ever existed.
        var stillQueued = await dbContext.Outbox
            .Where(entry => entry.EntityType == SyncEntityType.Note)
            .Select(entry => entry.LocalId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var received = 0;
        foreach (var incoming in feed.Changed)
        {
            var existing = await dbContext.Notes.FirstOrDefaultAsync(
                note => note.ServerId == incoming.Id, cancellationToken);

            if (existing is not null && stillQueued.Contains(existing.LocalId))
            {
                continue;
            }

            CopyInto(existing ?? NewLocalNote(dbContext, incoming.Id), incoming);
            received++;
        }

        var removed = 0;
        foreach (var deletedId in feed.DeletedIds)
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

        await SyncCursors.WriteAsync(dbContext, SyncEntityType.Note, feed.Cursor, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return (received, removed);
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
        note.IsPinned = incoming.IsPinned;
        note.LastSyncedAtUtc = _timeProvider.GetUtcNow();
    }
}
