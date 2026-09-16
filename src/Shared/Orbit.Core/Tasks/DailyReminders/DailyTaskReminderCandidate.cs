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
/// <param name="ComesRoundAgain">
/// Whether this entry is work that happens again every day rather than one errand being asked about
/// until it is done - see <see cref="IDailyTaskReminderRepository.GetEligibleAsync"/>, which is the only
/// place that decides it. One that comes round again is brought back when its reminder fires; one that
/// does not is left exactly as the reader left it, deadline included, and stops being asked about the
/// moment it is ticked off or crossed out.
/// </param>
public sealed record DailyTaskReminderCandidate(
    Guid TaskItemId,
    Guid TaskListId,
    Guid UserId,
    string TaskListTitle,
    string Description,
    DateTimeOffset? DueDateUtc,
    NotificationChannel NotificationChannel,
    TimeOnly TimeOfDay,
    bool ComesRoundAgain = false);
