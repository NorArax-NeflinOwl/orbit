using Microsoft.EntityFrameworkCore;
using Orbit.Contracts.Notes;
using Orbit.Core.Sync;
using Orbit.Mobile.Crypto;
using Orbit.Mobile.Sync;

namespace Orbit.Mobile.Data;

/// <summary>
/// A note as the reader has just left it - everything a save writes down, and nothing that identifies
/// which note it is. The same shape as <see cref="TaskListContent"/>, deliberately: the two
/// repositories are written alike so a change made to one is obvious in the other.
/// </summary>
/// <param name="Priority">
/// How much it matters, by name. Carried because a save writes the whole note: left out, it answered
/// "Normal" and took the reader's own answer with it - see LocalNote.Priority.
/// </param>
/// <param name="IsPrivate">
/// Whether the words above may only ever be read by their owner. Part of the content rather than a
/// setting beside it, because turning it on is what decides where they are written - see
/// LocalNoteRepository.WriteContentAsync.
/// </param>
public sealed record NoteContent(
    string Title, IReadOnlyList<NoteContentLineDto> Content, string Priority, bool IsPrivate = false);

/// <summary>
/// Every read and write a screen performs on notes. Reads come from SQLite and never from the API, and
/// each write records its own outbox entry in the same transaction as the change itself - a local edit
/// that was applied but not queued would be silently lost at the next pull, which is the worst failure
/// this layer could have.
/// </summary>
public sealed class LocalNoteRepository
{
    private readonly IDbContextFactory<OrbitLocalDbContext> _dbContextFactory;
    private readonly TimeProvider _timeProvider;
    private readonly INetworkStatus _networkStatus;
    private readonly PrivateContentSealer _privateContent;

    /// <summary>
    /// Takes a factory rather than a context because there is no request to scope one to: screens come
    /// and go, and a context held for the life of a screen accumulates every entity it ever loaded and
    /// keeps a SQLite connection open behind it.
    /// </summary>
    public LocalNoteRepository(
        IDbContextFactory<OrbitLocalDbContext> dbContextFactory, TimeProvider timeProvider, INetworkStatus networkStatus,
        PrivateContentSealer privateContent)
    {
        _dbContextFactory = dbContextFactory;
        _timeProvider = timeProvider;
        _networkStatus = networkStatus;
        _privateContent = privateContent;
    }

    public async Task<IReadOnlyList<LocalNote>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var notes = await dbContext.Notes
            .AsNoTracking()
            .OrderByDescending(note => note.IsPinned)
            .ThenByDescending(note => note.UpdatedAtUtc)
            .ToListAsync(cancellationToken);

        await OpenPrivateContentAsync(notes, cancellationToken);
        return notes;
    }

    /// <summary>
    /// Which notes still have changes waiting to go out. The screen marks these, so a user who wrote
    /// something on a train can see the app is holding it rather than wondering whether it was saved.
    /// </summary>
    public async Task<IReadOnlySet<Guid>> GetPendingNoteLocalIdsAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var localIds = await dbContext.Outbox
            .Where(entry => entry.EntityType == SyncEntityType.Note)
            .Select(entry => entry.LocalId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return localIds.ToHashSet();
    }

    public async Task<LocalNote?> FindAsync(Guid localId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var note = await dbContext.Notes.AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.LocalId == localId, cancellationToken);

        if (note is not null)
        {
            await OpenPrivateContentAsync([note], cancellationToken);
        }

        return note;
    }

    /// <summary>
    /// Puts a private note's real title and lines back on the rows handed to a screen, in place of the
    /// empty columns the store keeps. Untracked rows on purpose - what is opened here is for reading,
    /// and writing it back is exactly what must never happen.
    ///
    /// A note this device cannot open keeps its empty columns and is marked
    /// <see cref="LocalNote.IsSealed"/> rather than throwing: a phone with no key still has to be able
    /// to show the rest of the list, and a note sealed under a replaced key pair is nobody's fault to
    /// fix from here.
    /// </summary>
    private async Task OpenPrivateContentAsync(IReadOnlyList<LocalNote> notes, CancellationToken cancellationToken)
    {
        var privateNotes = notes.Where(note => note.IsPrivate).ToList();
        if (privateNotes.Count == 0)
        {
            // Nothing to open, and so nothing to ask the phone's keystore for - a list with no private
            // note in it must not pay for the key, nor fail without one.
            return;
        }

        PrivateContentKey key;
        try
        {
            key = await _privateContent.UnlockAsync(cancellationToken);
        }
        catch (EncryptionKeyLockedException)
        {
            MarkSealed(privateNotes);
            return;
        }

        using (key)
        {
            foreach (var note in privateNotes)
            {
                Open(key, note);
            }
        }
    }

    private static void Open(PrivateContentKey key, LocalNote note)
    {
        if (note.EncryptedContent is not { } encryptedContent
            || key.Open(encryptedContent, SealedContentSerializerContext.Default.SealedNote) is not { } opened)
        {
            note.IsSealed = true;
            return;
        }

        note.Title = opened.Title;
        note.Content = opened.Content;
        note.IsSealed = false;
    }

    private static void MarkSealed(IReadOnlyList<LocalNote> notes)
    {
        foreach (var note in notes)
        {
            note.IsSealed = true;
        }
    }

    public async Task<LocalNote> CreateAsync(
        string title, IReadOnlyList<NoteContentLineDto> content, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = _timeProvider.GetUtcNow();
        var note = new LocalNote
        {
            LocalId = Guid.NewGuid(),
            ServerId = null,
            Title = title,
            Content = content,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        dbContext.Notes.Add(note);
        Enqueue(dbContext, note.LocalId, OutboxOperation.Create, now);
        await dbContext.SaveChangesAsync(cancellationToken);
        return note;
    }

    /// <summary>
    /// Takes a copy of a note so it can be written on with no connection - see
    /// <see cref="LocalNote.CopyOfLocalId"/> for why that is offered at all.
    ///
    /// The copy is this phone's own: not shared, not yet on the server, and therefore editable by the
    /// ordinary rules. It stays on this phone until the review answers what it is - only "keep both"
    /// makes it a note in its own right, and pushing it before then would put a second note on the
    /// server that two of the three answers would immediately have to take away again.
    ///
    /// Null when there is nothing to copy. A private note is refused, sealed or not: what would be
    /// copied is either ciphertext this device cannot open, or words that are only allowed to exist
    /// sealed - and a copy is written in the clear. The policy never asks for one anyway, since a
    /// private note is shared with nobody and so is editable offline already.
    /// </summary>
    public async Task<LocalNote?> CopyForEditingAsync(Guid originalLocalId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (await dbContext.Notes.FirstOrDefaultAsync(candidate => candidate.LocalId == originalLocalId, cancellationToken)
            is not { IsSealed: false, IsPrivate: false } original)
        {
            return null;
        }

        var now = _timeProvider.GetUtcNow();
        var copy = new LocalNote
        {
            LocalId = Guid.NewGuid(),
            ServerId = null,
            Title = original.Title,
            Content = original.Content,
            Priority = original.Priority,
            CopyOfLocalId = original.LocalId,
            CopiedAtUtc = now,
            // What the original said now, kept so a review can tell a change from a collision.
            CopyBaseTitle = original.Title,
            CopyBaseContent = original.Content,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        dbContext.Notes.Add(copy);
        await dbContext.SaveChangesAsync(cancellationToken);
        return copy;
    }

    /// <summary>Copies of <paramref name="originalLocalId"/> still waiting to be reviewed or kept.</summary>
    public async Task<IReadOnlyList<LocalNote>> GetCopiesOfAsync(
        Guid originalLocalId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Notes.AsNoTracking()
            .Where(note => note.CopyOfLocalId == originalLocalId)
            .OrderByDescending(note => note.CopiedAtUtc)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Every copy this phone has taken and not yet resolved, newest first - what the review screen opens
    /// on once there is a connection again.
    /// </summary>
    public async Task<IReadOnlyList<LocalNote>> GetCopiesAwaitingReviewAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Notes.AsNoTracking()
            .Where(note => note.CopyOfLocalId != null && !note.IsKeptCopy)
            .OrderByDescending(note => note.CopiedAtUtc)
            .ToListAsync(cancellationToken);
    }

    /// <summary>Everything kept on purpose after a review - what the History screen lists.</summary>
    public async Task<IReadOnlyList<LocalNote>> GetKeptCopiesAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Notes.AsNoTracking()
            .Where(note => note.IsKeptCopy)
            .OrderByDescending(note => note.CopiedAtUtc)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Puts the copy's words onto the note it came from and drops the copy. What "keep mine" means.
    ///
    /// The original is written through the ordinary update path, so it is queued, locked and refused by
    /// exactly the rules any other edit is - a review with a connection is just an edit made late.
    /// </summary>
    public async Task<LocalWriteOutcome> ApplyCopyAsync(Guid copyLocalId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (await dbContext.Notes.FirstOrDefaultAsync(candidate => candidate.LocalId == copyLocalId, cancellationToken)
            is not { CopyOfLocalId: { } originalLocalId } copy)
        {
            return LocalWriteOutcome.NotFound;
        }

        if (await dbContext.Notes.FirstOrDefaultAsync(
                candidate => candidate.LocalId == originalLocalId, cancellationToken) is not { } original)
        {
            return LocalWriteOutcome.NotFound;
        }

        if (!OfflineEditPolicy.IsAllowed(original, _networkStatus))
        {
            return LocalWriteOutcome.RefusedWhileOffline;
        }

        var now = _timeProvider.GetUtcNow();
        original.Title = copy.Title;
        original.Content = copy.Content;
        original.UpdatedAtUtc = now;
        Enqueue(dbContext, original.LocalId, OutboxOperation.Update, now, original.ServerId);

        RemoveWithItsQueue(dbContext, copy);
        await dbContext.SaveChangesAsync(cancellationToken);
        return LocalWriteOutcome.Applied;
    }

    /// <summary>Drops the copy and leaves the original as it stands. What "keep theirs" means.</summary>
    public async Task<LocalWriteOutcome> DiscardCopyAsync(Guid copyLocalId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (await dbContext.Notes.FirstOrDefaultAsync(
                candidate => candidate.LocalId == copyLocalId && candidate.CopyOfLocalId != null, cancellationToken)
            is not { } copy)
        {
            return LocalWriteOutcome.NotFound;
        }

        RemoveWithItsQueue(dbContext, copy);
        await dbContext.SaveChangesAsync(cancellationToken);
        return LocalWriteOutcome.Applied;
    }

    /// <summary>
    /// Leaves both, which is the third answer a review can give. The copy stops being one under review
    /// and becomes a note in its own right - it keeps pointing at what it came from, which is all the
    /// History screen needs to say where it came from.
    /// </summary>
    public async Task<LocalWriteOutcome> KeepCopyAsync(Guid copyLocalId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (await dbContext.Notes.FirstOrDefaultAsync(
                candidate => candidate.LocalId == copyLocalId && candidate.CopyOfLocalId != null, cancellationToken)
            is not { } copy)
        {
            return LocalWriteOutcome.NotFound;
        }

        var now = _timeProvider.GetUtcNow();
        copy.IsKeptCopy = true;
        copy.UpdatedAtUtc = now;
        Enqueue(dbContext, copy.LocalId, OutboxOperation.Create, now);
        await dbContext.SaveChangesAsync(cancellationToken);
        return LocalWriteOutcome.Applied;
    }

    /// <summary>
    /// Takes a copy out along with anything queued about it. An unreviewed copy queues nothing, but one
    /// resolved twice - kept and then applied - would have a Create waiting, and leaving that behind
    /// would push a note the reader has just discarded.
    /// </summary>
    private static void RemoveWithItsQueue(OrbitLocalDbContext dbContext, LocalNote copy)
    {
        var queued = dbContext.Outbox
            .Where(entry => entry.EntityType == SyncEntityType.Note && entry.LocalId == copy.LocalId);

        dbContext.Outbox.RemoveRange(queued);
        dbContext.Notes.Remove(copy);
    }

    /// <inheritdoc cref="LocalTaskListRepository.CanEditAsync"/>
    public async Task<bool> CanEditAsync(Guid localId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var note = await dbContext.Notes.AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.LocalId == localId, cancellationToken);

        return note is not null && OfflineEditPolicy.IsAllowed(note, _networkStatus);
    }

    /// <summary>
    /// Refuses rather than queues when the offline policy forbids it - see LocalWriteOutcome. A copy
    /// still awaiting review is written to this phone and queued for nobody, because what it is has not
    /// been decided yet; the review is what sends it, if it sends it at all.
    /// </summary>
    public async Task<LocalWriteOutcome> UpdateAsync(
        Guid localId, NoteContent content, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (await dbContext.Notes.FirstOrDefaultAsync(candidate => candidate.LocalId == localId, cancellationToken) is not { } note)
        {
            return LocalWriteOutcome.NotFound;
        }

        if (!OfflineEditPolicy.IsAllowed(note, _networkStatus))
        {
            return LocalWriteOutcome.RefusedWhileOffline;
        }

        var now = _timeProvider.GetUtcNow();
        await WriteContentAsync(note, content, cancellationToken);
        note.Priority = content.Priority;
        note.UpdatedAtUtc = now;

        if (!IsAwaitingReview(note))
        {
            Enqueue(dbContext, localId, OutboxOperation.Update, now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return LocalWriteOutcome.Applied;
    }

    /// <summary>
    /// Writes what the reader left, in the form the note is allowed to be held in. A private one keeps
    /// its words in the sealed payload and leaves the readable columns empty - what the server stores
    /// and what this phone stores are then the same thing, which is the whole promise of IsPrivate.
    ///
    /// Turning private off restores the readable columns and drops the payload, so the sealed copy does
    /// not linger behind a note that is no longer private.
    /// </summary>
    /// <exception cref="EncryptionKeyLockedException">
    /// This device has no key to seal with. Deliberately thrown rather than refused quietly: the caller
    /// sends the reader to the key gate, exactly as chat does, and saving the words in the clear instead
    /// would break the promise the checkbox just made.
    /// </exception>
    private async Task WriteContentAsync(LocalNote note, NoteContent content, CancellationToken cancellationToken)
    {
        note.IsPrivate = content.IsPrivate;
        note.IsSealed = false;

        if (!content.IsPrivate)
        {
            note.Title = content.Title;
            note.Content = content.Content;
            note.EncryptedCiphertext = null;
            note.EncryptedNonce = null;
            return;
        }

        using var key = await _privateContent.UnlockAsync(cancellationToken);
        var sealedContent = key.Seal(
            new SealedNote(content.Title, content.Content), SealedContentSerializerContext.Default.SealedNote);

        note.Title = string.Empty;
        note.Content = [];
        note.EncryptedCiphertext = sealedContent.Ciphertext;
        note.EncryptedNonce = sealedContent.Nonce;
    }

    /// <summary>
    /// Writes down where the server says this note now sits. Deliberately touches nothing else - not
    /// UpdatedAtUtc, not the outbox: pinning is not a change to the note, and queueing it as one would
    /// send the whole note back on the next replay.
    /// </summary>
    public async Task MarkPinnedAsync(Guid localId, bool isPinned, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (await dbContext.Notes.FirstOrDefaultAsync(note => note.LocalId == localId, cancellationToken) is not { } note)
        {
            return;
        }

        note.IsPinned = isPinned;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<LocalWriteOutcome> DeleteAsync(Guid localId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (await dbContext.Notes.FirstOrDefaultAsync(candidate => candidate.LocalId == localId, cancellationToken) is not { } note)
        {
            return LocalWriteOutcome.NotFound;
        }

        if (!OfflineEditPolicy.IsAllowed(note, _networkStatus))
        {
            return LocalWriteOutcome.RefusedWhileOffline;
        }

        dbContext.Notes.Remove(note);

        // A note the server never saw has nothing to delete there. Dropping what was queued for it also
        // stops replay from creating the note the user has just thrown away.
        if (note.ServerId is null)
        {
            dbContext.Outbox.RemoveRange(dbContext.Outbox.Where(
                entry => entry.EntityType == SyncEntityType.Note && entry.LocalId == localId));
        }
        else
        {
            Enqueue(dbContext, localId, OutboxOperation.Delete, _timeProvider.GetUtcNow(), note.ServerId);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return LocalWriteOutcome.Applied;
    }

    /// <summary>A copy taken offline that no review has answered yet - see CopyForEditingAsync.</summary>
    private static bool IsAwaitingReview(LocalNote note) => note is { CopyOfLocalId: not null, IsKeptCopy: false };

    private static void Enqueue(
        OrbitLocalDbContext dbContext, Guid noteLocalId, OutboxOperation operation, DateTimeOffset queuedAtUtc,
        Guid? noteServerId = null)
        => dbContext.Outbox.Add(new OutboxEntry
        {
            EntityType = SyncEntityType.Note,
            LocalId = noteLocalId,
            ServerId = noteServerId,
            Operation = operation,
            QueuedAtUtc = queuedAtUtc
        });
}
