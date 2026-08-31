using Microsoft.EntityFrameworkCore;
using Orbit.Contracts.Tasks;
using Orbit.Core.Sync;
using Orbit.Mobile.Crypto;
using Orbit.Mobile.Sync;

namespace Orbit.Mobile.Data;

/// <summary>
/// A task list as the reader has just left it - everything a save writes down, and nothing that
/// identifies which list it is. Grouped rather than passed as four arguments beside the id, which is
/// what adding the priority would have made it.
/// </summary>
/// <param name="Priority">
/// How much the list matters, by name - "Low", "Normal" or "High", as ItemPriority spells them. Carried
/// because a save writes the whole list: left out, it would go back to Normal every time somebody
/// renamed the list from a phone.
/// </param>
/// <param name="IsPrivate"><inheritdoc cref="NoteContent.IsPrivate" path="/summary"/></param>
public sealed record TaskListContent(
    string Title, IReadOnlyList<TaskItemDto> Items, bool IsGroup, string Priority, bool IsPrivate = false);

/// <summary>
/// Every read and write a screen performs on task lists. The same shape as
/// <see cref="LocalNoteRepository"/>, deliberately: each write records its own outbox entry in the same
/// transaction as the change, because a local edit that was applied but not queued is silently lost at
/// the next pull.
/// </summary>
public sealed class LocalTaskListRepository : ICopyReviewStore
{
    private readonly IDbContextFactory<OrbitLocalDbContext> _dbContextFactory;
    private readonly TimeProvider _timeProvider;
    private readonly INetworkStatus _networkStatus;
    private readonly PrivateContentSealer _privateContent;

    public LocalTaskListRepository(
        IDbContextFactory<OrbitLocalDbContext> dbContextFactory, TimeProvider timeProvider, INetworkStatus networkStatus,
        PrivateContentSealer privateContent)
    {
        _dbContextFactory = dbContextFactory;
        _timeProvider = timeProvider;
        _networkStatus = networkStatus;
        _privateContent = privateContent;
    }

    public async Task<IReadOnlyList<LocalTaskList>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var taskLists = await dbContext.TaskLists
            .AsNoTracking()
            // Pinned lists first, then most recently changed - the order the web client shows them in.
            .OrderByDescending(taskList => taskList.IsPinned)
            .ThenByDescending(taskList => taskList.UpdatedAtUtc)
            .ToListAsync(cancellationToken);

        await OpenPrivateContentAsync(taskLists, cancellationToken);
        return taskLists;
    }

    public async Task<LocalTaskList?> FindAsync(Guid localId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var taskList = await dbContext.TaskLists.AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.LocalId == localId, cancellationToken);

        if (taskList is not null)
        {
            await OpenPrivateContentAsync([taskList], cancellationToken);
        }

        return taskList;
    }

    /// <inheritdoc cref="LocalNoteRepository.OpenPrivateContentAsync"/>
    private async Task OpenPrivateContentAsync(IReadOnlyList<LocalTaskList> taskLists, CancellationToken cancellationToken)
    {
        var privateLists = taskLists.Where(taskList => taskList.IsPrivate).ToList();
        if (privateLists.Count == 0)
        {
            return;
        }

        PrivateContentKey key;
        try
        {
            key = await _privateContent.UnlockAsync(cancellationToken);
        }
        catch (EncryptionKeyLockedException)
        {
            foreach (var taskList in privateLists)
            {
                taskList.IsSealed = true;
            }

            return;
        }

        using (key)
        {
            foreach (var taskList in privateLists)
            {
                Open(key, taskList);
            }
        }
    }

    private static void Open(PrivateContentKey key, LocalTaskList taskList)
    {
        if (taskList.EncryptedContent is not { } encryptedContent
            || key.Open(encryptedContent, SealedContentSerializerContext.Default.SealedTaskList) is not { } opened)
        {
            taskList.IsSealed = true;
            return;
        }

        taskList.Title = opened.Title;
        taskList.Items = opened.Items;
        // Worked out here for the same reason the domain works it out: the server saw no items to
        // derive it from, so what it sent back for a private list means nothing.
        taskList.IsCompleted = opened.Items.Count > 0 && opened.Items.All(item => item.IsCompleted);
        taskList.IsSealed = false;
    }

    /// <summary>
    /// Whether this list may be changed right now - the same question <see cref="UpdateAsync"/> asks
    /// before writing, so a screen and the write it leads to can never disagree. Asking by attempting a
    /// write would queue one, which is the opposite of what a read-only check is for.
    /// </summary>
    public async Task<bool> CanEditAsync(Guid localId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var taskList = await dbContext.TaskLists.AsNoTracking()
            .FirstOrDefaultAsync(list => list.LocalId == localId, cancellationToken);

        return taskList is not null && OfflineEditPolicy.IsAllowed(taskList, _networkStatus);
    }

    /// <summary>Which lists still have changes waiting to go out, so the screen can mark them.</summary>
    public async Task<IReadOnlySet<Guid>> GetPendingLocalIdsAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var localIds = await dbContext.Outbox
            .Where(entry => entry.EntityType == SyncEntityType.TaskList)
            .Select(entry => entry.LocalId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return localIds.ToHashSet();
    }

    public async Task<LocalTaskList> CreateAsync(
        string title, IReadOnlyList<TaskItemDto> items, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = _timeProvider.GetUtcNow();
        var taskList = new LocalTaskList
        {
            LocalId = Guid.NewGuid(),
            ServerId = null,
            Title = title,
            Items = items,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        dbContext.TaskLists.Add(taskList);
        Enqueue(dbContext, taskList.LocalId, OutboxOperation.Create, now);
        await dbContext.SaveChangesAsync(cancellationToken);
        return taskList;
    }

    /// <summary>Refuses rather than queues when the offline policy forbids it - see LocalWriteOutcome.</summary>
    /// <param name="isGroup">
    /// Whether it gathers the lists its items link to rather than holding work of its own. Part of the
    /// update rather than its own call, unlike pinning: this changes what the list <i>is</i>.
    /// </param>
    public async Task<LocalWriteOutcome> UpdateAsync(
        Guid localId, TaskListContent content, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (await dbContext.TaskLists.FirstOrDefaultAsync(list => list.LocalId == localId, cancellationToken) is not { } taskList)
        {
            return LocalWriteOutcome.NotFound;
        }

        if (!OfflineEditPolicy.IsAllowed(taskList, _networkStatus))
        {
            return LocalWriteOutcome.RefusedWhileOffline;
        }

        var now = _timeProvider.GetUtcNow();
        await WriteContentAsync(taskList, content, cancellationToken);
        taskList.IsGroup = content.IsGroup;
        taskList.Priority = content.Priority;
        taskList.UpdatedAtUtc = now;
        // A list is done when every item is - the same rule the server applies. Worked out from what
        // was handed in rather than from the row, which holds no items at all when the list is private.
        taskList.IsCompleted = content.Items.Count > 0 && content.Items.All(item => item.IsCompleted);

        // A copy still awaiting review is written to this phone and queued for nobody: what it is has
        // not been decided yet, and the review is what sends it - see LocalNoteRepository.UpdateAsync.
        if (!CopiesForEditing.IsAwaitingReview(taskList))
        {
            Enqueue(dbContext, localId, OutboxOperation.Update, now);
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        return LocalWriteOutcome.Applied;
    }

    /// <inheritdoc cref="LocalNoteRepository.WriteContentAsync"/>
    private async Task WriteContentAsync(LocalTaskList taskList, TaskListContent content, CancellationToken cancellationToken)
    {
        taskList.IsPrivate = content.IsPrivate;
        taskList.IsSealed = false;

        // Named here rather than by the server, for every list and not only a sealed one - see
        // WithIdentity.
        var items = WithIdentity(content.Items);

        if (!content.IsPrivate)
        {
            taskList.Title = content.Title;
            taskList.Items = items;
            taskList.EncryptedCiphertext = null;
            taskList.EncryptedNonce = null;
            return;
        }

        using var key = await _privateContent.UnlockAsync(cancellationToken);
        var sealedContent = key.Seal(
            new SealedTaskList(content.Title, items),
            SealedContentSerializerContext.Default.SealedTaskList);

        taskList.Title = string.Empty;
        taskList.Items = [];
        taskList.EncryptedCiphertext = sealedContent.Ciphertext;
        taskList.EncryptedNonce = sealedContent.Nonce;
    }

    /// <summary>
    /// Gives every entry an id as it is written, whatever kind of list it is on.
    ///
    /// It began as a rule for sealed lists alone: the server never sees a private list's entries, so
    /// without this each stayed empty, every entry had the same id, and ticking one ticked them all.
    /// It now applies to all of them, which is a change of principle rather than of scope - <b>an entry
    /// is named by whoever writes it</b>, so one written with no connection has an identity from the
    /// moment it exists instead of being renamed by its first successful push.
    ///
    /// That rename was the thing making offline work hard: nothing on this phone could point at an
    /// entry and still be pointing at it after a sync. The server accepts these ids and settles a
    /// collision by renaming both sides - see Orbit.Core.Tasks.TaskItemIdentity.
    /// </summary>
    private static IReadOnlyList<TaskItemDto> WithIdentity(IReadOnlyList<TaskItemDto> items)
        => [.. items.Select(item => item.Id == Guid.Empty ? item with { Id = Guid.NewGuid() } : item)];

    /// <inheritdoc cref="LocalNoteRepository.MarkPinnedAsync"/>
    public async Task MarkPinnedAsync(Guid localId, bool isPinned, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (await dbContext.TaskLists.FirstOrDefaultAsync(list => list.LocalId == localId, cancellationToken) is not { } taskList)
        {
            return;
        }

        taskList.IsPinned = isPinned;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<LocalWriteOutcome> DeleteAsync(Guid localId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (await dbContext.TaskLists.FirstOrDefaultAsync(list => list.LocalId == localId, cancellationToken) is not { } taskList)
        {
            return LocalWriteOutcome.NotFound;
        }

        if (!OfflineEditPolicy.IsAllowed(taskList, _networkStatus))
        {
            return LocalWriteOutcome.RefusedWhileOffline;
        }

        dbContext.TaskLists.Remove(taskList);

        // A list the server never saw has nothing to delete there, and dropping what was queued for it
        // stops replay creating the list the user has just thrown away.
        if (taskList.ServerId is null)
        {
            dbContext.Outbox.RemoveRange(dbContext.Outbox.Where(
                entry => entry.EntityType == SyncEntityType.TaskList && entry.LocalId == localId));
        }
        else
        {
            Enqueue(dbContext, localId, OutboxOperation.Delete, _timeProvider.GetUtcNow(), taskList.ServerId);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return LocalWriteOutcome.Applied;
    }

    /// <inheritdoc cref="LocalNoteRepository.CopyForEditingAsync"/>
    public async Task<LocalTaskList?> CopyForEditingAsync(Guid originalLocalId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (await dbContext.TaskLists.FirstOrDefaultAsync(candidate => candidate.LocalId == originalLocalId, cancellationToken)
            is not { IsSealed: false, IsPrivate: false } original)
        {
            return null;
        }

        var now = _timeProvider.GetUtcNow();
        var copy = new LocalTaskList
        {
            LocalId = Guid.NewGuid(),
            ServerId = null,
            Title = original.Title,
            // The entries keep their ids - see ICopyReviewStore.KeepCopyAsync for why, and for where
            // they are given up again.
            Items = original.Items,
            IsGroup = original.IsGroup,
            IsCompleted = original.IsCompleted,
            LinkedWarehouseId = original.LinkedWarehouseId,
            Priority = original.Priority,
            Status = original.Status,
            CopyOfLocalId = original.LocalId,
            CopiedAtUtc = now,
            CopyBaseTitle = original.Title,
            CopyBaseLines = Describe(original.Items),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        dbContext.TaskLists.Add(copy);
        CopiesForEditing.Announce(dbContext, CopyKind.TaskList, copy.LocalId, original.Title, now);
        await dbContext.SaveChangesAsync(cancellationToken);
        return copy;
    }

    public CopyKind Kind => CopyKind.TaskList;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CopyUnderReview>> GetCopiesAwaitingReviewAsync(
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await DescribeAllAsync(dbContext, CopiesForEditing.AwaitingReviewAsync<LocalTaskList>, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CopyUnderReview>> GetKeptCopiesAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await DescribeAllAsync(dbContext, CopiesForEditing.KeptAsync<LocalTaskList>, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CopyUnderReview>> GetHistoryOfAsync(
        Guid localId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await DescribeAllAsync(
            dbContext,
            (context, token) => CopiesForEditing.HistoryOfAsync<LocalTaskList>(context, localId, token),
            cancellationToken);
    }

    /// <inheritdoc cref="LocalNoteRepository.ApplyCopyAsync"/>
    public async Task<LocalWriteOutcome> ApplyCopyAsync(Guid copyLocalId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (await CopiesForEditing.FindCopyAsync<LocalTaskList>(dbContext, copyLocalId, cancellationToken)
            is not { CopyOfLocalId: { } originalLocalId } copy)
        {
            return LocalWriteOutcome.NotFound;
        }

        if (await dbContext.TaskLists.FirstOrDefaultAsync(
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
        original.Items = copy.Items;
        original.IsGroup = copy.IsGroup;
        original.Priority = copy.Priority;
        original.IsCompleted = copy.Items.Count > 0 && copy.Items.All(item => item.IsCompleted);
        original.UpdatedAtUtc = now;
        Enqueue(dbContext, original.LocalId, OutboxOperation.Update, now, original.ServerId);

        CopiesForEditing.Remove(dbContext, copy, SyncEntityType.TaskList);
        await dbContext.SaveChangesAsync(cancellationToken);
        return LocalWriteOutcome.Applied;
    }

    /// <inheritdoc/>
    public async Task<LocalWriteOutcome> DiscardCopyAsync(Guid copyLocalId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (await CopiesForEditing.FindCopyAsync<LocalTaskList>(dbContext, copyLocalId, cancellationToken) is not { } copy)
        {
            return LocalWriteOutcome.NotFound;
        }

        CopiesForEditing.Remove(dbContext, copy, SyncEntityType.TaskList);
        await dbContext.SaveChangesAsync(cancellationToken);
        return LocalWriteOutcome.Applied;
    }

    /// <inheritdoc/>
    public async Task<LocalWriteOutcome> KeepCopyAsync(Guid copyLocalId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (await CopiesForEditing.FindCopyAsync<LocalTaskList>(dbContext, copyLocalId, cancellationToken) is not { } copy)
        {
            return LocalWriteOutcome.NotFound;
        }

        var now = _timeProvider.GetUtcNow();
        // Its entries stop being the original's entries the moment this becomes a list of its own.
        copy.Items = [.. copy.Items.Select(item => item with { Id = Guid.NewGuid() })];
        copy.UpdatedAtUtc = now;
        CopiesForEditing.Keep(dbContext, copy, SyncEntityType.TaskList, now);
        await dbContext.SaveChangesAsync(cancellationToken);
        return LocalWriteOutcome.Applied;
    }

    /// <inheritdoc cref="LocalNoteRepository.DescribeAllAsync"/>
    private async Task<IReadOnlyList<CopyUnderReview>> DescribeAllAsync(
        OrbitLocalDbContext dbContext,
        Func<OrbitLocalDbContext, CancellationToken, Task<IReadOnlyList<LocalTaskList>>> read,
        CancellationToken cancellationToken)
    {
        var described = new List<CopyUnderReview>();
        foreach (var copy in await read(dbContext, cancellationToken))
        {
            var original = await dbContext.TaskLists.AsNoTracking().FirstOrDefaultAsync(
                candidate => candidate.LocalId == copy.CopyOfLocalId, cancellationToken);

            described.Add(new CopyUnderReview(
                CopyKind.TaskList, copy.LocalId, copy.CopyOfLocalId!.Value,
                original?.Title is { Length: > 0 } title ? title : copy.CopyBaseTitle,
                copy.CopiedAtUtc ?? copy.CreatedAtUtc,
                copy.CopyBaseLines, Describe(copy.Items),
                original is null ? null : Describe(original.Items),
                copy.IsKeptCopy));
        }

        return described;
    }

    /// <summary>
    /// A list's entries as a review reads them: ticked or not, what it says, and when it is due. The
    /// date is written plainly rather than in the reader's own format, because these lines are compared
    /// as text - a snapshot taken under one language must not read as a change under another.
    /// </summary>
    private static IReadOnlyList<string> Describe(IReadOnlyList<TaskItemDto> items)
        => [.. items.Select(item => item switch
        {
            { IsCompleted: true, DueDateUtc: { } completedDue } => $"[x] {item.Description} ({completedDue:yyyy-MM-dd})",
            { IsCompleted: true } => $"[x] {item.Description}",
            { DueDateUtc: { } due } => $"[ ] {item.Description} ({due:yyyy-MM-dd})",
            _ => $"[ ] {item.Description}"
        })];

    private static void Enqueue(
        OrbitLocalDbContext dbContext, Guid localId, OutboxOperation operation, DateTimeOffset queuedAtUtc,
        Guid? serverId = null)
        => dbContext.Outbox.Add(new OutboxEntry
        {
            EntityType = SyncEntityType.TaskList,
            LocalId = localId,
            ServerId = serverId,
            Operation = operation,
            QueuedAtUtc = queuedAtUtc
        });
}
