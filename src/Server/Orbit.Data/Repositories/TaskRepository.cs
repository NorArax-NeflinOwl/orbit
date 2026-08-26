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

    public async Task<IReadOnlyList<TaskList>> GetAllAsync(Guid userId, CancellationToken cancellationToken)
    {
        // SQLite can't translate ORDER BY on a DateTimeOffset column, so the sort has to happen in
        // memory after fetching (see the EF Core NotSupportedException this avoids) - same reason
        // NoteRepository sorts after ToListAsync.
        var entities = await _dbContext.Tasks
            .AsNoTracking()
            .Include(task => task.Items)
            .Where(task => task.UserId == userId)
            .ToListAsync(cancellationToken);

        return entities
            .OrderByDescending(task => task.UpdatedAtUtc)
            .Select(ToDomain)
            .ToList();
    }

    public async Task<TaskList?> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Tasks
            .AsNoTracking()
            .Include(task => task.Items)
            .FirstOrDefaultAsync(task => task.Id == id && task.UserId == userId, cancellationToken);

        return entity is null ? null : ToDomain(entity);
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
        entity.IsCompleted = taskList.IsCompleted;
        entity.IsGroup = taskList.IsGroup;
        entity.Priority = taskList.Priority.ToString();
        entity.IsPinned = taskList.IsPinned;
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
        var existingItems = await _dbContext.Set<TaskItemEntity>()
            .Where(item => item.TaskId == taskList.Id)
            .ToListAsync(cancellationToken);
        _dbContext.RemoveRange(existingItems);
        _dbContext.AddRange(taskList.Items.Select(item => ToItemEntity(item, taskList.Id)));
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
            entity.Items.Select(ToItemDomain).ToList(),
            entity.IsGroup,
            entity.IsPrivate,
            ToEncryptedPayload(entity.EncryptedCiphertext, entity.EncryptedNonce),
            entity.CreatedAtUtc,
            entity.UpdatedAtUtc,
            entity.LockedByUserId,
            entity.LockedByUserName,
            entity.LockExpiresAtUtc,
            Enum.TryParse<TaskListPriority>(entity.Priority, out var priority) ? priority : TaskListPriority.Normal,
            entity.IsPinned);

    private static TaskItem ToItemDomain(TaskItemEntity entity)
        => TaskItem.FromPersistence(
            entity.Id,
            entity.Description,
            entity.DueDateUtc,
            entity.IsCompleted,
            entity.LinkedTaskListId,
            Enum.Parse<NotificationChannel>(entity.OverdueNotificationChannel, ignoreCase: true),
            entity.RemindDaily,
            Enum.Parse<NotificationChannel>(entity.DailyReminderNotificationChannel, ignoreCase: true),
            TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(entity.DailyReminderTimeOfDayMinutes)));

    private static TaskEntity ToEntity(TaskList taskList)
        => new()
        {
            Id = taskList.Id,
            UserId = taskList.UserId,
            Title = taskList.Title,
            IsCompleted = taskList.IsCompleted,
            IsGroup = taskList.IsGroup,
            Priority = taskList.Priority.ToString(),
            IsPinned = taskList.IsPinned,
            IsPrivate = taskList.IsPrivate,
            EncryptedCiphertext = taskList.EncryptedContent?.Ciphertext,
            EncryptedNonce = taskList.EncryptedContent?.Nonce,
            LockedByUserId = taskList.LockedByUserId,
            LockedByUserName = taskList.LockedByUserName,
            LockExpiresAtUtc = taskList.LockExpiresAtUtc,
            CreatedAtUtc = taskList.CreatedAtUtc,
            UpdatedAtUtc = taskList.UpdatedAtUtc,
            Items = taskList.Items.Select(item => ToItemEntity(item, taskList.Id)).ToList()
        };

    private static TaskItemEntity ToItemEntity(TaskItem item, Guid taskId)
        => new()
        {
            Id = item.Id,
            TaskId = taskId,
            Description = item.Description,
            DueDateUtc = item.DueDateUtc,
            IsCompleted = item.IsCompleted,
            LinkedTaskListId = item.LinkedTaskListId,
            OverdueNotificationChannel = item.OverdueNotificationChannel.ToString(),
            RemindDaily = item.RemindDaily,
            DailyReminderNotificationChannel = item.DailyReminderNotificationChannel.ToString(),
            DailyReminderTimeOfDayMinutes = item.DailyReminderTimeOfDay.Hour * 60 + item.DailyReminderTimeOfDay.Minute
        };
}
