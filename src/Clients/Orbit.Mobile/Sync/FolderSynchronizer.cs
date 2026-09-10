using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orbit.Contracts.Folders;
using Orbit.Core.Sync;
using Orbit.Mobile.Api;
using Orbit.Mobile.Data;

namespace Orbit.Mobile.Sync;

/// <summary>
/// Brings the tabs somebody made and the server back into step: send what was done offline, then take
/// what changed elsewhere - the same order and the same reasons as <see cref="NoteSynchronizer"/>.
///
/// The one thing that differs from the other four is the pull. Folders have no change feed and need
/// none: an account has a handful of them and each is a name and a page, so this asks for all of them
/// and reconciles against what it holds. That also makes deletion free - a folder missing from the
/// answer has gone, with no tombstone to carry it.
/// </summary>
public sealed class FolderSynchronizer
{
    private readonly IDbContextFactory<OrbitLocalDbContext> _dbContextFactory;
    private readonly FoldersClient _foldersClient;
    private readonly TimeProvider _timeProvider;
    private readonly SyncGate _syncGate;
    private readonly ILogger<FolderSynchronizer> _logger;

    public FolderSynchronizer(
        IDbContextFactory<OrbitLocalDbContext> dbContextFactory, FoldersClient foldersClient,
        TimeProvider timeProvider, SyncGate syncGate, ILogger<FolderSynchronizer> logger)
    {
        _dbContextFactory = dbContextFactory;
        _foldersClient = foldersClient;
        _timeProvider = timeProvider;
        _syncGate = syncGate;
        _logger = logger;
    }

    /// <inheritdoc cref="NoteSynchronizer.SynchroniseAsync"/>
    public Task<SyncResult> SynchroniseAsync(CancellationToken cancellationToken = default)
        => _syncGate.RunAsync(SyncEntityType.Folder, () => RunAsync(cancellationToken), cancellationToken);

    private async Task<SyncResult> RunAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var push = await OutboxReplay.RunAsync(
            dbContext, SyncEntityType.Folder,
            (entry, token) => SendAsync(dbContext, entry, token), _timeProvider, _logger, cancellationToken);

        try
        {
            var pull = await PullAsync(dbContext, cancellationToken);
            return new SyncResult(push.Sent, pull.Received, pull.RemovedLocally, push.GivenUp, ReachedTheServer: true);
        }
        catch (Exception exception) when (SyncFailure.IsWorthRetrying(exception, cancellationToken))
        {
            _logger.LogInformation("Could not reach the server to pull folders ({Reason})", exception.Message);
            return push.Sent > 0
                ? new SyncResult(push.Sent, 0, 0, push.GivenUp, ReachedTheServer: true)
                : SyncResult.NeverGotThrough(push.GivenUp);
        }
    }

    private async Task<SendResult> SendAsync(
        OrbitLocalDbContext dbContext, OutboxEntry entry, CancellationToken cancellationToken)
    {
        if (entry.Operation is OutboxOperation.Delete)
        {
            if (entry.ServerId is not { } serverId)
            {
                return SendResult.Abandoned;
            }

            await _foldersClient.DeleteAsync(serverId, cancellationToken);
            return SendResult.Sent;
        }

        var folder = await dbContext.Folders.FirstOrDefaultAsync(
            candidate => candidate.LocalId == entry.LocalId, cancellationToken);

        if (folder is null)
        {
            // Deleted locally before this ever went out - see LocalFolderRepository.DeleteAsync, which
            // drops the queue for a folder the server never saw.
            return SendResult.Abandoned;
        }

        return entry.Operation is OutboxOperation.Create
            ? await SendCreateAsync(folder, cancellationToken)
            : await SendRenameAsync(folder, cancellationToken);
    }

    private async Task<SendResult> SendCreateAsync(LocalFolder folder, CancellationToken cancellationToken)
    {
        if (folder.ServerId is not null)
        {
            // Already created - a duplicate create would make a second tab out of one.
            return SendResult.Abandoned;
        }

        var created = await _foldersClient.CreateAsync(
            new CreateFolderRequest(folder.Name, folder.Scope), cancellationToken);

        if (created is null)
        {
            return SendResult.Refused;
        }

        folder.ServerId = created.Id;
        return SendResult.Sent;
    }

    private async Task<SendResult> SendRenameAsync(LocalFolder folder, CancellationToken cancellationToken)
    {
        if (folder.ServerId is not { } serverId)
        {
            // Its create is still queued ahead of this and has not succeeded yet; that create carries
            // whatever the name has become by then.
            return SendResult.Abandoned;
        }

        var outcome = await _foldersClient.RenameAsync(
            serverId, new RenameFolderRequest(folder.Name), cancellationToken);

        if (outcome is not WriteOutcome.Applied)
        {
            _logger.LogInformation("The server refused a rename of folder {ServerId}: {Outcome}", serverId, outcome);
            return SendResult.Refused;
        }

        return SendResult.Sent;
    }

    /// <summary>
    /// Every folder the account has, reconciled against what this phone holds. A folder still waiting to
    /// be created here has no server id and is left alone - it is not missing from the answer, it was
    /// never in the question.
    /// </summary>
    private async Task<(int Received, int RemovedLocally)> PullAsync(
        OrbitLocalDbContext dbContext, CancellationToken cancellationToken)
    {
        var theirs = await _foldersClient.GetAllAsync(cancellationToken);

        var stillQueued = await dbContext.Outbox
            .Where(entry => entry.EntityType == SyncEntityType.Folder)
            .Select(entry => entry.LocalId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var mine = await dbContext.Folders.ToListAsync(cancellationToken);
        var received = 0;

        foreach (var incoming in theirs)
        {
            var existing = mine.FirstOrDefault(folder => folder.ServerId == incoming.Id);

            if (existing is not null && stillQueued.Contains(existing.LocalId))
            {
                continue;
            }

            if (existing is null)
            {
                existing = new LocalFolder { LocalId = Guid.NewGuid(), ServerId = incoming.Id };
                dbContext.Folders.Add(existing);
                mine.Add(existing);
            }

            existing.Name = incoming.Name;
            existing.Scope = incoming.Scope;
            existing.CreatedAtUtc = incoming.CreatedAtUtc;
            existing.UpdatedAtUtc = incoming.UpdatedAtUtc;
            received++;
        }

        var removed = 0;
        var theirIds = theirs.Select(folder => folder.Id).ToHashSet();

        foreach (var folder in mine.Where(folder => folder.ServerId is { } id && !theirIds.Contains(id)))
        {
            if (stillQueued.Contains(folder.LocalId))
            {
                continue;
            }

            // Gone elsewhere. Everything filed under it is unfiled here too, which is what the server
            // did as it dropped the folder - a phone that only dropped the row would go on showing
            // those notes under a tab that no longer exists.
            await UnfileAsync(dbContext, folder.LocalId, cancellationToken);
            dbContext.Folders.Remove(folder);
            removed++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return (received, removed);
    }

    private static async Task UnfileAsync(
        OrbitLocalDbContext dbContext, Guid folderLocalId, CancellationToken cancellationToken)
    {
        foreach (var note in await dbContext.Notes.Where(note => note.FolderId == folderLocalId).ToListAsync(cancellationToken))
        {
            note.FolderId = null;
        }

        foreach (var taskList in await dbContext.TaskLists.Where(list => list.FolderId == folderLocalId).ToListAsync(cancellationToken))
        {
            taskList.FolderId = null;
        }
    }
}
