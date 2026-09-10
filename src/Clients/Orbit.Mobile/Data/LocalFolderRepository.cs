using Microsoft.EntityFrameworkCore;
using Orbit.Core.Folders;
using Orbit.Core.Sync;
using Orbit.Mobile.Sync;

namespace Orbit.Mobile.Data;

/// <summary>
/// Every read and write a screen performs on folders. Reads come from SQLite and never from the API,
/// and each write records its own outbox entry in the same transaction as the change itself - see
/// <see cref="LocalNoteRepository"/>, which says why that pairing is the important part.
///
/// Deleting a folder takes the tab away and leaves everything that was under it, which is what the
/// server does too: a folder is a place to put things, and getting rid of the place is not a decision
/// to get rid of them. The things fall back to a built-in folder on their own, because that is what
/// having no folder means - see <see cref="FolderPlacement"/>.
/// </summary>
public sealed class LocalFolderRepository
{
    private readonly IDbContextFactory<OrbitLocalDbContext> _dbContextFactory;
    private readonly TimeProvider _timeProvider;

    public LocalFolderRepository(IDbContextFactory<OrbitLocalDbContext> dbContextFactory, TimeProvider timeProvider)
    {
        _dbContextFactory = dbContextFactory;
        _timeProvider = timeProvider;
    }

    /// <summary>Every folder this account has, whichever page it is a tab on, in the order it reads.</summary>
    public async Task<IReadOnlyList<LocalFolder>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Folders
            .AsNoTracking()
            .OrderBy(folder => folder.Name)
            .ToListAsync(cancellationToken);
    }

    /// <summary>The tabs on one page - see <see cref="FolderScope"/>, which is why a folder has a page.</summary>
    public async Task<IReadOnlyList<LocalFolder>> GetAllAsync(
        FolderScope scope, CancellationToken cancellationToken = default)
    {
        var wanted = scope.ToString();
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Folders
            .AsNoTracking()
            .Where(folder => folder.Scope == wanted)
            .OrderBy(folder => folder.Name)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Makes one. It exists on this phone the moment it is asked for, connection or none: a folder is a
    /// name, so there is nothing about it that only a server can decide, and a reader who cannot make a
    /// tab until they have signal cannot tidy up on a train.
    /// </summary>
    public async Task<LocalFolder> CreateAsync(
        string name, FolderScope scope, CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();
        var folder = new LocalFolder
        {
            LocalId = Guid.NewGuid(),
            Name = name.Trim(),
            Scope = scope.ToString(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        dbContext.Folders.Add(folder);
        Enqueue(dbContext, folder.LocalId, OutboxOperation.Create, now);
        await dbContext.SaveChangesAsync(cancellationToken);
        return folder;
    }

    public async Task<LocalWriteOutcome> RenameAsync(
        Guid localId, string name, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (await dbContext.Folders.FirstOrDefaultAsync(folder => folder.LocalId == localId, cancellationToken) is not { } stored)
        {
            return LocalWriteOutcome.NotFound;
        }

        stored.Name = name.Trim();
        stored.UpdatedAtUtc = _timeProvider.GetUtcNow();

        // Nothing to send about a folder the server has never seen: the create still queued in front of
        // this one carries whatever the name has become by the time it goes out.
        if (stored.ServerId is not null)
        {
            Enqueue(dbContext, localId, OutboxOperation.Update, stored.UpdatedAtUtc, stored.ServerId);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return LocalWriteOutcome.Applied;
    }

    /// <summary>
    /// Takes the tab away. Everything filed under it is unfiled here as well as there, which is what
    /// makes the two agree without waiting for a pull: the server empties the folder as it drops it,
    /// and a phone that only dropped the row would keep showing its notes under a tab that has gone.
    /// </summary>
    public async Task<LocalWriteOutcome> DeleteAsync(Guid localId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (await dbContext.Folders.FirstOrDefaultAsync(folder => folder.LocalId == localId, cancellationToken) is not { } stored)
        {
            return LocalWriteOutcome.NotFound;
        }

        foreach (var note in await dbContext.Notes.Where(note => note.FolderId == localId).ToListAsync(cancellationToken))
        {
            note.FolderId = null;
        }

        foreach (var taskList in await dbContext.TaskLists.Where(list => list.FolderId == localId).ToListAsync(cancellationToken))
        {
            taskList.FolderId = null;
        }

        dbContext.Folders.Remove(stored);

        // A folder the server never saw has nothing to delete there, and dropping what was queued for
        // it also stops replay from making the folder somebody has just thrown away.
        if (stored.ServerId is null)
        {
            dbContext.Outbox.RemoveRange(dbContext.Outbox.Where(
                entry => entry.EntityType == SyncEntityType.Folder && entry.LocalId == localId));
        }
        else
        {
            Enqueue(dbContext, localId, OutboxOperation.Delete, _timeProvider.GetUtcNow(), stored.ServerId);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return LocalWriteOutcome.Applied;
    }

    private static void Enqueue(
        OrbitLocalDbContext dbContext, Guid localId, OutboxOperation operation, DateTimeOffset queuedAtUtc,
        Guid? serverId = null)
        => dbContext.Outbox.Add(new OutboxEntry
        {
            EntityType = SyncEntityType.Folder,
            LocalId = localId,
            ServerId = serverId,
            Operation = operation,
            QueuedAtUtc = queuedAtUtc
        });
}
