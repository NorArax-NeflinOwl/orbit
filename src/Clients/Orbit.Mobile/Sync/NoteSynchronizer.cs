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
            (entry, token) => SendAsync(dbContext, entry, token), _timeProvider, _logger, cancellationToken);

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

        return entry.Operation switch
        {
            OutboxOperation.Create => await SendCreateAsync(dbContext, note, cancellationToken),
            OutboxOperation.File => await SendFilingAsync(dbContext, note, cancellationToken),
            _ => await SendUpdateAsync(note, cancellationToken)
        };
    }

    /// <summary>
    /// Which folder the server should be told, for a note filed into one of this phone's. Null both for
    /// a note in no folder and for one whose folder has since gone - and a folder the server has not
    /// been told about yet stops the send instead, so the filing waits for it rather than being lost.
    /// </summary>
    private static async Task<Guid?> ServerFolderIdAsync(
        OrbitLocalDbContext dbContext, Guid? folderLocalId, CancellationToken cancellationToken)
    {
        if (folderLocalId is not { } localId)
        {
            return null;
        }

        var folder = await dbContext.Folders.FirstOrDefaultAsync(
            candidate => candidate.LocalId == localId, cancellationToken);

        return folder is null ? null : folder.ServerId ?? throw new FolderNotOnTheServerYet(localId);
    }

    /// <summary>
    /// Puts the note in its folder, or takes it out of one. Its own request to its own endpoint - see
    /// NotesClient.FileAsync, and MoveToFolderRequest, which says why a save must not carry this.
    /// </summary>
    private async Task<SendResult> SendFilingAsync(
        OrbitLocalDbContext dbContext, LocalNote note, CancellationToken cancellationToken)
    {
        if (note.ServerId is not { } serverId)
        {
            // Its create is still queued ahead of this, and that create carries the folder itself.
            return SendResult.Abandoned;
        }

        var outcome = await _notesClient.FileAsync(
            serverId, await ServerFolderIdAsync(dbContext, note.FolderId, cancellationToken), cancellationToken);

        if (outcome is not WriteOutcome.Applied)
        {
            _logger.LogInformation("The server refused a filing of note {ServerId}: {Outcome}", serverId, outcome);
            return SendResult.Refused;
        }

        return SendResult.Sent;
    }

    private async Task<SendResult> SendCreateAsync(
        OrbitLocalDbContext dbContext, LocalNote note, CancellationToken cancellationToken)
    {
        if (note.ServerId is not null)
        {
            // Already created - a duplicate create would make a second note out of one.
            return SendResult.Abandoned;
        }

        note.ServerId = await _notesClient.CreateAsync(
            // A private note's words are in EncryptedContent and its readable fields are empty, which is
            // how the row is already stored - see LocalNoteRepository.WriteContentAsync.
            //
            // The folder travels on the create and only on the create: a note the server has never seen
            // has nothing to file, so LocalNoteRepository queues no filing for one and this is where it
            // would otherwise be lost.
            new CreateNoteRequest(
                note.Title, note.Content, note.IsPrivate, note.EncryptedContent, note.Priority,
                await ServerFolderIdAsync(dbContext, note.FolderId, cancellationToken),
                // Null for a note held since before tags - "not provided", which keeps what the server has.
                note.Tags),
            cancellationToken);
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
            serverId,
            // The priority travels with every save, because a save writes the whole note: left out, it
            // answered "Normal" and took the reader's own answer with it - see LocalNote.Priority.
            // The tags as this phone holds them - null, "not known", for a note held since before tags,
            // which keeps whatever the server has rather than emptying it. See LocalNote.Tags.
            new UpdateNoteRequest(note.Title, note.Content, note.IsPrivate, note.EncryptedContent, note.Priority, note.Tags),
            cancellationToken);

        if (outcome is not WriteOutcome.Applied)
        {
            _logger.LogInformation("The server refused an offline edit of note {ServerId}: {Outcome}", serverId, outcome);
            return SendResult.Refused;
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

        // A folder is known here by an id of this phone's own, and arrives named by the server's - see
        // LocalFolder.LocalId. Read once for the run rather than per note.
        var foldersByServerId = await dbContext.Folders
            .Where(folder => folder.ServerId != null)
            .ToDictionaryAsync(folder => folder.ServerId!.Value, folder => folder.LocalId, cancellationToken);

        var received = 0;
        foreach (var incoming in feed.Changed)
        {
            var existing = await dbContext.Notes.FirstOrDefaultAsync(
                note => note.ServerId == incoming.Id, cancellationToken);

            if (existing is not null && stillQueued.Contains(existing.LocalId))
            {
                continue;
            }

            CopyInto(existing ?? NewLocalNote(dbContext, incoming.Id), incoming, foldersByServerId);
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

    private void CopyInto(LocalNote note, NoteDto incoming, IReadOnlyDictionary<Guid, Guid> foldersByServerId)
    {
        // A folder this phone has not heard of leaves the note unfiled rather than pointing at nothing,
        // which is the same answer FolderPlacement gives for an id it does not know: back under whichever
        // built-in folder the note belongs to, rather than gone from every tab.
        note.FolderId = incoming.FolderId is { } folderServerId
            && foldersByServerId.TryGetValue(folderServerId, out var folderLocalId)
                ? folderLocalId
                : null;
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
        note.OwnerUserId = incoming.OriginalOwnerUserId;
        note.IsPinned = incoming.IsPinned;
        note.Priority = incoming.Priority;
        // As the server said it, null included: a server written before tags says nothing, and that is
        // "not known" here too rather than "none". Empty for a private note, whose tags come out of its seal.
        note.Tags = incoming.Tags;
        note.LastSyncedAtUtc = _timeProvider.GetUtcNow();
    }
}
