using Orbit.Core.Notifications;

namespace Orbit.Core.Tasks.DailyReminders;

/// <summary>A single task item's daily reminder that has come due today and hasn't been sent yet.</summary>
/// <param name="ReminderDate">
/// The local calendar date this reminder is for - paired with TaskItemId as the claim key (see
/// IDailyTaskReminderRepository), so a task item reminded about yesterday is eligible again today.
/// </param>
/// <param name="ComesRoundAgain">
/// Whether the entry is brought back before the reminder goes out - see
/// <see cref="DailyTaskReminderCandidate.ComesRoundAgain"/>.
/// </param>
public sealed record DueDailyTaskReminder(
    Guid TaskItemId,
    Guid TaskListId,
    Guid UserId,
    string TaskListTitle,
    string Description,
    DateTimeOffset? DueDateUtc,
    NotificationChannel NotificationChannel,
    DateOnly ReminderDate,
    bool ComesRoundAgain = false);
