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
            (entry, token) => SendAsync(dbContext, entry, token), _logger, cancellationToken);

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
            new CreateTaskRequest(taskList.Title, ToRequests(taskList.Items), taskList.IsGroup, taskList.IsPrivate,
                Priority: taskList.Priority),
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
            new UpdateTaskRequest(taskList.Title, ToRequests(taskList.Items), taskList.IsGroup, taskList.IsPrivate,
                Priority: taskList.Priority),
            cancellationToken);

        if (outcome is not WriteOutcome.Applied)
        {
            _logger.LogInformation("The server refused an offline edit of task list {ServerId}: {Outcome}", serverId, outcome);
            return SendResult.Abandoned;
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
        taskList.Items = incoming.Items;
        taskList.IsCompleted = incoming.IsCompleted;
        taskList.IsGroup = incoming.IsGroup;
        taskList.IsPrivate = incoming.IsPrivate;
        taskList.EncryptedCiphertext = incoming.EncryptedContent?.Ciphertext;
        taskList.EncryptedNonce = incoming.EncryptedContent?.Nonce;
        taskList.CreatedAtUtc = incoming.CreatedAtUtc;
        taskList.UpdatedAtUtc = incoming.UpdatedAtUtc;
        taskList.IsShared = incoming.IsShared;
        taskList.SharedByUserName = incoming.SharedByUserName;
        taskList.IsSharedWithOthers = incoming.IsSharedWithOthers;
        taskList.AccessLevel = incoming.AccessLevel;
        taskList.Priority = incoming.Priority;
        taskList.Status = incoming.Status;
        taskList.IsPinned = incoming.IsPinned;
        taskList.LastSyncedAtUtc = _timeProvider.GetUtcNow();
    }

    /// <summary>
    /// An item as the server takes it back. The only difference from what came down is that a request
    /// carries no id - the server assigns those - so a round trip is not lossless by accident.
    /// </summary>
    private static IReadOnlyList<TaskItemRequest> ToRequests(IReadOnlyList<TaskItemDto> items)
        => items.Select(item => new TaskItemRequest(
            item.Description, item.DueDateUtc, item.IsCompleted, item.LinkedTaskListId,
            item.OverdueNotificationChannel, item.RemindDaily, item.DailyReminderNotificationChannel,
            item.DailyReminderTimeOfDay)).ToList();
}
