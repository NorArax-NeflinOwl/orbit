namespace Orbit.Core.Tasks.OverdueNotifications;

/// <summary>
/// A single checklist entry that has become overdue, carrying just enough of its owning
/// <see cref="TaskList"/> to build and route a push notification about it - see
/// <see cref="IOverdueTaskNotificationRepository"/> and <see cref="OverdueTaskNotificationScheduler"/>.
/// A lighter-weight projection than the full <see cref="TaskItem"/>/<see cref="TaskList"/> domain
/// model, since this cross-user query has no reason to load every other item on the list.
/// </summary>
public sealed record OverdueTaskItem(
    Guid TaskItemId, Guid TaskListId, Guid UserId, string TaskListTitle, string Description, DateTimeOffset DueDateUtc);
