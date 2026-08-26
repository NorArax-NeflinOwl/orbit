using Microsoft.EntityFrameworkCore;
using Orbit.Contracts.Notes;
using Orbit.Core.Sync;

namespace Orbit.Mobile.Data;

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

    /// <summary>
    /// Takes a factory rather than a context because there is no request to scope one to: screens come
    /// and go, and a context held for the life of a screen accumulates every entity it ever loaded and
    /// keeps a SQLite connection open behind it.
    /// </summary>
    public LocalNoteRepository(IDbContextFactory<OrbitLocalDbContext> dbContextFactory, TimeProvider timeProvider)
    {
        _dbContextFactory = dbContextFactory;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<LocalNote>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Notes
            .AsNoTracking()
            .OrderByDescending(note => note.UpdatedAtUtc)
            .ToListAsync(cancellationToken);
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
        return await dbContext.Notes.AsNoTracking().FirstOrDefaultAsync(note => note.LocalId == localId, cancellationToken);
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

    /// <summary>False when the note is not there - it was deleted underneath the caller.</summary>
    public async Task<bool> UpdateAsync(
        Guid localId, string title, IReadOnlyList<NoteContentLineDto> content, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (await dbContext.Notes.FirstOrDefaultAsync(note => note.LocalId == localId, cancellationToken) is not { } note)
        {
            return false;
        }

        var now = _timeProvider.GetUtcNow();
        note.Title = title;
        note.Content = content;
        note.UpdatedAtUtc = now;

        Enqueue(dbContext, localId, OutboxOperation.Update, now);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid localId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (await dbContext.Notes.FirstOrDefaultAsync(note => note.LocalId == localId, cancellationToken) is not { } note)
        {
            return false;
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
        return true;
    }

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
