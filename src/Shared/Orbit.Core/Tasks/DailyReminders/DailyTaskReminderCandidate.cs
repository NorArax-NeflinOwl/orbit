using Orbit.Core.Notifications;

namespace Orbit.Core.Tasks.DailyReminders;

/// <summary>
/// A single checklist entry with "remind daily" enabled, carrying just enough of its owning
/// <see cref="TaskList"/> to decide whether today's reminder is due yet and, once it is, to build and
/// route a notification about it - see <see cref="IDailyTaskReminderRepository"/> and
/// <see cref="DailyTaskReminderScheduler"/>. A lighter-weight projection than the full
/// <see cref="TaskItem"/>/<see cref="TaskList"/> domain model, mirroring
/// <see cref="Orbit.Core.Tasks.OverdueNotifications.OverdueTaskItem"/> for the overdue notification.
/// </summary>
public sealed record DailyTaskReminderCandidate(
    Guid TaskItemId,
    Guid TaskListId,
    Guid UserId,
    string TaskListTitle,
    string Description,
    DateTimeOffset? DueDateUtc,
    NotificationChannel NotificationChannel,
    TimeOnly TimeOfDay);
