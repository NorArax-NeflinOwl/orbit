using Microsoft.EntityFrameworkCore;
using Orbit.Core.Notifications;
using Orbit.Core.Abstractions;
using Orbit.Core.Tasks;
using Orbit.Data.Entities;

namespace Orbit.Data.Repositories;

public sealed class TaskRepository : ITaskRepository
{
    private readonly OrbitDbContext _dbContext;

    public TaskRepository(OrbitDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<TaskList>> GetAllAsync(
        Guid userId, DateTimeOffset? updatedSinceUtc, CancellationToken cancellationToken)
    {
        var query = _dbContext.Tasks
            .AsNoTracking()
            .Include(task => task.Items).ThenInclude(item => item.LinkedTaskLists)
            .Include(task => task.Items).ThenInclude(item => item.Categories)
            .Where(task => task.UserId == userId);

        // Narrowed in the database when the caller only wants what changed. A client catching up asks
        // for a delta; fetching everything and dropping most of it here saved the wire and nothing else.
        if (updatedSinceUtc is not null)
        {
            query = query.Where(task => task.UpdatedAtUtc >= updatedSinceUtc.Value);
        }

        var entities = await query.ToListAsync(cancellationToken);

        return entities
            .OrderByDescending(task => task.UpdatedAtUtc)
            .Select(ToDomain)
            .ToList();
    }

    public async Task<TaskList?> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Tasks
            .AsNoTracking()
            .Include(task => task.Items).ThenInclude(item => item.LinkedTaskLists)
            .Include(task => task.Items).ThenInclude(item => item.Categories)
            .FirstOrDefaultAsync(task => task.Id == id && task.UserId == userId, cancellationToken);

        return entity is null ? null : ToDomain(entity);
    }

    public async Task<IReadOnlyList<TaskList>> GetHoldingItemsAsync(
        Guid userId, Guid exceptListId, IReadOnlyList<Guid> itemIds, CancellationToken cancellationToken)
    {
        if (itemIds.Count == 0)
        {
            return [];
        }

        var entities = await _dbContext.Tasks
            .AsNoTracking()
            .Include(task => task.Items).ThenInclude(item => item.LinkedTaskLists)
            .Include(task => task.Items).ThenInclude(item => item.Categories)
            .Where(task => task.UserId == userId
                && task.Id != exceptListId
                && task.Items.Any(item => itemIds.Contains(item.Id)))
            .ToListAsync(cancellationToken);

        return entities.Select(ToDomain).ToList();
    }

    public async Task AddAsync(TaskList taskList, CancellationToken cancellationToken)
    {
        _dbContext.Tasks.Add(ToEntity(taskList));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(TaskList taskList, CancellationToken cancellationToken)
    {
        await StageUpdateAsync(taskList, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Stages every list's changes on the same DbContext, then saves them all in one SaveChangesAsync
    /// call - a single call is already atomic across everything the context is tracking, so this is
    /// enough to keep e.g. a cross-list item move from partially applying if something fails midway,
    /// without needing an explicit database transaction.
    /// </summary>
    public async Task UpdateManyAsync(IReadOnlyList<TaskList> taskLists, CancellationToken cancellationToken)
    {
        foreach (var taskList in taskLists)
        {
            await StageUpdateAsync(taskList, cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task StageUpdateAsync(TaskList taskList, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Tasks.FirstAsync(task => task.Id == taskList.Id, cancellationToken);
        entity.Title = taskList.Title;
        entity.Description = taskList.Description;
        entity.IsCompleted = taskList.IsCompleted;
        entity.IsGroup = taskList.IsGroup;
        entity.Priority = taskList.Priority.ToString();
        entity.IsPinned = taskList.IsPinned;
        entity.LinkedWarehouseId = taskList.LinkedWarehouseId;
        entity.IsPrivate = taskList.IsPrivate;
        entity.EncryptedCiphertext = taskList.EncryptedContent?.Ciphertext;
        entity.EncryptedNonce = taskList.EncryptedContent?.Nonce;
        entity.LockedByUserId = taskList.LockedByUserId;
        entity.LockedByUserName = taskList.LockedByUserName;
        entity.LockExpiresAtUtc = taskList.LockExpiresAtUtc;
        entity.UpdatedAtUtc = taskList.UpdatedAtUtc;

        // The domain always replaces the whole checklist on update rather than diffing individual
        // items (see TaskList.Update). Clearing and re-populating the tracked Items navigation
        // instead of this explicit remove/add made EF Core treat the freshly-created items as
        // updates to rows that don't exist yet (DbUpdateConcurrencyException: 0 rows affected),
        // since their ids are already non-default by the time they reach the change tracker.
        // The children come along: without them the change tracker knows only about the parent rows, so
        // it leaves their links and categories to the database's own cascade - and the inserts for the
        // replacements can then reach the server before that cascade has run, against a primary key
        // (item + linked list) the old rows still hold. That is a 23505 on a save nobody thought was
        // risky, which is exactly how it turned up: on a lock heartbeat.
        var existingItems = await _dbContext.Set<TaskItemEntity>()
            .Include(item => item.LinkedTaskLists)
            .Include(item => item.Categories)
            .Where(item => item.TaskId == taskList.Id)
            .ToListAsync(cancellationToken);
        _dbContext.RemoveRange(existingItems);
        _dbContext.AddRange(taskList.Items.Select((item, position) => ToItemEntity(item, taskList.Id, position)));
    }

    /// <summary>
    /// The three columns a lock is, and nothing else - see ITaskRepository.UpdateLockAsync. Neither
    /// UpdatedAtUtc nor the entries are touched: holding the page open is not a change to the list, and
    /// saying it was would move the card under "recently updated" every twenty seconds.
    /// </summary>
    public async Task UpdateLockAsync(TaskList taskList, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Tasks.FirstAsync(task => task.Id == taskList.Id, cancellationToken);
        entity.LockedByUserId = taskList.LockedByUserId;
        entity.LockedByUserName = taskList.LockedByUserName;
        entity.LockExpiresAtUtc = taskList.LockExpiresAtUtc;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Tasks
            .FirstOrDefaultAsync(task => task.Id == id && task.UserId == userId, cancellationToken);
        if (entity is null)
        {
            return;
        }

        // No need to load or remove the Items navigation separately - the foreign key from
        // TaskItemEntity to this row was created with ON DELETE CASCADE (see OrbitDbContext), so SQLite
        // removes them itself once this row goes away.
        _dbContext.Tasks.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Both columns are written together or not at all, so either alone means no sealed content.</summary>
    private static EncryptedPayload? ToEncryptedPayload(string? ciphertext, string? nonce)
        // Blank counts as absent, not just null: a row half-written before EncryptedPayload started
        // checking its own parts would otherwise fail inside that check while being read, which is the
        // one place a stored row must never throw.
        => !string.IsNullOrWhiteSpace(ciphertext) && !string.IsNullOrWhiteSpace(nonce)
            ? new EncryptedPayload(ciphertext, nonce)
            : null;

    private static TaskList ToDomain(TaskEntity entity)
        => TaskList.FromPersistence(
            entity.Id,
            entity.UserId,
            entity.Title,
            // Ordered here rather than trusted from the navigation: a delete-and-reinsert leaves the
            // rows in no particular order, and the reader's own order is the one that matters.
            entity.Items.OrderBy(item => item.Position).Select(ToItemDomain).ToList(),
            entity.IsGroup,
            entity.IsPrivate,
            ToEncryptedPayload(entity.EncryptedCiphertext, entity.EncryptedNonce),
            entity.CreatedAtUtc,
            entity.UpdatedAtUtc,
            entity.LockedByUserId,
            entity.LockedByUserName,
            entity.LockExpiresAtUtc,
            Enum.TryParse<ItemPriority>(entity.Priority, out var priority) ? priority : ItemPriority.Normal,
            entity.IsPinned, entity.LinkedWarehouseId, entity.Description);

    private static TaskItem ToItemDomain(TaskItemEntity entity)
        => TaskItem.FromPersistence(
            entity.Id,
            entity.Description,
            entity.DueDateUtc,
            entity.IsCompleted,
            [.. entity.LinkedTaskLists.OrderBy(link => link.Position).Select(link => link.LinkedTaskListId)],
            Enum.Parse<NotificationChannel>(entity.OverdueNotificationChannel, ignoreCase: true),
            entity.RemindDaily,
            Enum.Parse<NotificationChannel>(entity.DailyReminderNotificationChannel, ignoreCase: true),
            TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(entity.DailyReminderTimeOfDayMinutes)),
            Enum.TryParse<TaskItemKind>(entity.Kind, out var kind) ? kind : TaskItemKind.Checklist,
            entity.Location, entity.LinkedCalendarEventId, entity.LinkedInventoryItemId,
            [.. entity.Categories.OrderBy(category => category.Position).Select(category => category.Category)]);

    private static TaskEntity ToEntity(TaskList taskList)
        => new()
        {
            Id = taskList.Id,
            UserId = taskList.UserId,
            Title = taskList.Title,
            Description = taskList.Description,
            IsCompleted = taskList.IsCompleted,
            IsGroup = taskList.IsGroup,
            Priority = taskList.Priority.ToString(),
            IsPinned = taskList.IsPinned,
            LinkedWarehouseId = taskList.LinkedWarehouseId,
            IsPrivate = taskList.IsPrivate,
            EncryptedCiphertext = taskList.EncryptedContent?.Ciphertext,
            EncryptedNonce = taskList.EncryptedContent?.Nonce,
            LockedByUserId = taskList.LockedByUserId,
            LockedByUserName = taskList.LockedByUserName,
            LockExpiresAtUtc = taskList.LockExpiresAtUtc,
            CreatedAtUtc = taskList.CreatedAtUtc,
            UpdatedAtUtc = taskList.UpdatedAtUtc,
            Items = taskList.Items.Select((item, position) => ToItemEntity(item, taskList.Id, position)).ToList()
        };

    private static TaskItemEntity ToItemEntity(TaskItem item, Guid taskId, int position)
        => new()
        {
            Id = item.Id,
            TaskId = taskId,
            Position = position,
            Description = item.Description,
            DueDateUtc = item.DueDateUtc,
            IsCompleted = item.IsCompleted,
            LinkedTaskLists = [.. item.LinkedTaskListIds.Select((linkedId, linkPosition) =>
                new TaskItemTaskListLinkEntity
                {
                    TaskItemId = item.Id,
                    LinkedTaskListId = linkedId,
                    Position = linkPosition
                })],
            OverdueNotificationChannel = item.OverdueNotificationChannel.ToString(),
            RemindDaily = item.RemindDaily,
            DailyReminderNotificationChannel = item.DailyReminderNotificationChannel.ToString(),
            DailyReminderTimeOfDayMinutes = item.DailyReminderTimeOfDay.Hour * 60 + item.DailyReminderTimeOfDay.Minute,
            Kind = item.Kind.ToString(),
            Location = item.Location,
            LinkedCalendarEventId = item.LinkedCalendarEventId,
            LinkedInventoryItemId = item.LinkedInventoryItemId,
            Categories = [.. item.Categories.Select((category, categoryPosition) =>
                new TaskItemCategoryEntity
                {
                    TaskItemId = item.Id,
                    Category = category,
                    Position = categoryPosition
                })]
        };
}
