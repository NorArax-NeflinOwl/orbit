using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orbit.Contracts.Tasks;
using Orbit.Core.Sync;
using Orbit.Mobile.Api;
using Orbit.Mobile.Data;

namespace Orbit.Mobile.Sync;

/// <summary>
/// Brings task lists and the server back into step. The second entity type on the sync spine, and what
/// the spine was factored for: everything that is not about task lists - replaying the queue in order,
/// classifying failures, remembering the cursor - is shared with notes rather than written again here.
///
/// What is genuinely per feature is small and visible: which requests a create, update and delete are,
/// and how a <see cref="TaskDto"/> becomes a local row.
/// </summary>
public sealed class TaskListSynchronizer
{
    private readonly IDbContextFactory<OrbitLocalDbContext> _dbContextFactory;
    private readonly TasksClient _tasksClient;
    private readonly TimeProvider _timeProvider;
    private readonly SyncGate _syncGate;
    private readonly ILogger<TaskListSynchronizer> _logger;

    public TaskListSynchronizer(
        IDbContextFactory<OrbitLocalDbContext> dbContextFactory, TasksClient tasksClient,
        TimeProvider timeProvider, SyncGate syncGate, ILogger<TaskListSynchronizer> logger)
    {
        _dbContextFactory = dbContextFactory;
        _tasksClient = tasksClient;
        _timeProvider = timeProvider;
        _syncGate = syncGate;
        _logger = logger;
    }

    /// <summary>Never throws for being offline - see NoteSynchronizer for why that is a rule here.</summary>
    public Task<SyncResult> SynchroniseAsync(CancellationToken cancellationToken = default)
        // Serialised rather than run alongside another - see SyncGate for what overlapping costs.
        => _syncGate.RunAsync(SyncEntityType.TaskList, () => RunAsync(cancellationToken), cancellationToken);

    private async Task<SyncResult> RunAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var push = await OutboxReplay.RunAsync(
            dbContext, SyncEntityType.TaskList,
            (entry, token) => SendAsync(dbContext, entry, token), _timeProvider, _logger, cancellationToken);

        try
        {
            var pull = await PullChangesAsync(dbContext, cancellationToken);
            return new SyncResult(push.Sent, pull.Received, pull.RemovedLocally, push.GivenUp, ReachedTheServer: true);
        }
        catch (Exception exception) when (SyncFailure.IsWorthRetrying(exception, cancellationToken))
        {
            _logger.LogInformation("Could not reach the server to pull task lists ({Reason})", exception.Message);
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

            await _tasksClient.DeleteAsync(serverId, cancellationToken);
            return SendResult.Sent;
        }

        var taskList = await dbContext.TaskLists.FirstOrDefaultAsync(
            candidate => candidate.LocalId == entry.LocalId, cancellationToken);

        if (taskList is null)
        {
            return SendResult.Abandoned;
        }

        return entry.Operation is OutboxOperation.Create
            ? await SendCreateAsync(taskList, cancellationToken)
            : await SendUpdateAsync(taskList, cancellationToken);
    }

    private async Task<SendResult> SendCreateAsync(LocalTaskList taskList, CancellationToken cancellationToken)
    {
        if (taskList.ServerId is not null)
        {
            // Already created - a duplicate create would make a second list out of one.
            return SendResult.Abandoned;
        }

        taskList.ServerId = await _tasksClient.CreateAsync(
            // A private list's title and entries are in EncryptedContent and its readable fields are
            // empty, which is how the row is already stored - see LocalTaskListRepository.
            new CreateTaskRequest(taskList.Title, ToRequests(taskList.Items), taskList.IsGroup, taskList.IsPrivate,
                taskList.EncryptedContent, taskList.Priority, taskList.Description),
            cancellationToken);
        taskList.LastSyncedAtUtc = _timeProvider.GetUtcNow();
        return SendResult.Sent;
    }

    private async Task<SendResult> SendUpdateAsync(LocalTaskList taskList, CancellationToken cancellationToken)
    {
        if (taskList.ServerId is not { } serverId)
        {
            // Its create is still queued ahead of this and has not succeeded yet.
            return SendResult.Abandoned;
        }

        var outcome = await _tasksClient.UpdateAsync(
            serverId,
            // Said rather than left out: null would mean "not provided" and keep whatever is stored, so
            // a description cleared on this phone would come back at the next pull - see CreateTaskRequest.
            new UpdateTaskRequest(taskList.Title, ToRequests(taskList.Items), taskList.IsGroup, taskList.IsPrivate,
                taskList.EncryptedContent, taskList.Priority, taskList.Description),
            cancellationToken);

        if (outcome is not WriteOutcome.Applied)
        {
            _logger.LogInformation("The server refused an offline edit of task list {ServerId}: {Outcome}", serverId, outcome);
            return SendResult.Refused;
        }

        taskList.LastSyncedAtUtc = _timeProvider.GetUtcNow();
        return SendResult.Sent;
    }

    private async Task<(int Received, int RemovedLocally)> PullChangesAsync(
        OrbitLocalDbContext dbContext, CancellationToken cancellationToken)
    {
        var cursor = await SyncCursors.ReadAsync(dbContext, SyncEntityType.TaskList, cancellationToken);
        var feed = await _tasksClient.GetChangesAsync(cursor, cancellationToken);

        // A list with changes still queued is the one thing the server's version must not overwrite.
        var stillQueued = await dbContext.Outbox
            .Where(entry => entry.EntityType == SyncEntityType.TaskList)
            .Select(entry => entry.LocalId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var received = 0;
        foreach (var incoming in feed.Changed)
        {
            var existing = await dbContext.TaskLists.FirstOrDefaultAsync(
                taskList => taskList.ServerId == incoming.Id, cancellationToken);

            if (existing is not null && stillQueued.Contains(existing.LocalId))
            {
                continue;
            }

            CopyInto(existing ?? NewLocalTaskList(dbContext, incoming.Id), incoming);
            received++;
        }

        var removed = 0;
        foreach (var deletedId in feed.DeletedIds)
        {
            var taskList = await dbContext.TaskLists.FirstOrDefaultAsync(
                candidate => candidate.ServerId == deletedId, cancellationToken);

            if (taskList is null || stillQueued.Contains(taskList.LocalId))
            {
                continue;
            }

            dbContext.TaskLists.Remove(taskList);
            removed++;
        }

        await SyncCursors.WriteAsync(dbContext, SyncEntityType.TaskList, feed.Cursor, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return (received, removed);
    }

    private static LocalTaskList NewLocalTaskList(OrbitLocalDbContext dbContext, Guid serverId)
    {
        var taskList = new LocalTaskList { LocalId = Guid.NewGuid(), ServerId = serverId };
        dbContext.TaskLists.Add(taskList);
        return taskList;
    }

    private void CopyInto(LocalTaskList taskList, TaskDto incoming)
    {
        taskList.Title = incoming.Title;
        taskList.Description = incoming.Description;
        taskList.Items = incoming.Items;
        taskList.IsCompleted = incoming.IsCompleted;
        taskList.IsGroup = incoming.IsGroup;
        taskList.LinkedWarehouseId = incoming.LinkedWarehouseId;
        taskList.IsPrivate = incoming.IsPrivate;
        taskList.EncryptedCiphertext = incoming.EncryptedContent?.Ciphertext;
        taskList.EncryptedNonce = incoming.EncryptedContent?.Nonce;
        taskList.CreatedAtUtc = incoming.CreatedAtUtc;
        taskList.UpdatedAtUtc = incoming.UpdatedAtUtc;
        taskList.IsShared = incoming.IsShared;
        taskList.SharedByUserName = incoming.SharedByUserName;
        taskList.IsSharedWithOthers = incoming.IsSharedWithOthers;
        taskList.AccessLevel = incoming.AccessLevel;
        taskList.OwnerUserId = incoming.OriginalOwnerUserId;
        taskList.Priority = incoming.Priority;
        taskList.Status = incoming.Status;
        taskList.IsPinned = incoming.IsPinned;
        taskList.LastSyncedAtUtc = _timeProvider.GetUtcNow();
    }

    /// <summary>
    /// An item as the server takes it back, <b>including its existing id</b>. That is not a detail: other
    /// things point at a task entry by id - an inventory item's open restock task, a daily reminder's
    /// "already sent today" record, an overdue notification - so a save that minted fresh ids would cut
    /// every one of them loose. See TaskItemRequest.Id, which exists for exactly this.
    ///
    /// Null for an entry added on this phone, which has no server id yet: <see cref="Guid.Empty"/> is
    /// what the local store writes for one, and sending it would be claiming an id nothing has.
    ///
    /// The kind, the place and the event it belongs to travel with it for the same reason: left off,
    /// every push from the phone turned an appointment set on the web back into a plain errand with
    /// nowhere to be.
    ///
    /// The shelf item is the same story and worse. TaskItem keeps LinkedInventoryItemId only for an
    /// Inventory entry and drops it otherwise, so a push that carried neither the link nor the kind cut
    /// a restock errand loose from the product it was about - on any save of any list holding one,
    /// without the reader touching the errand at all.
    /// </summary>
    private static IReadOnlyList<TaskItemRequest> ToRequests(IReadOnlyList<TaskItemDto> items)
        => items.Select(item => new TaskItemRequest(
            item.Description, item.Id == Guid.Empty ? null : item.Id, item.DueDateUtc, item.IsCompleted,
            // The new field, always: the old single one carries only the first list, so a save from
            // this phone would quietly drop the rest of an entry standing for several.
            LinkedTaskListId: null, item.OverdueNotificationChannel, item.RemindDaily,
            item.DailyReminderNotificationChannel, item.DailyReminderTimeOfDay,
            item.Kind, item.Location, item.LinkedCalendarEventId, item.LinkedInventoryItemId,
            item.AllLinkedTaskListIds)).ToList();
}
