namespace Orbit.Core.Tasks.DailyReminders;

/// <summary>
/// Finds task items whose "remind daily" time of day has been reached today and haven't been reminded
/// about yet today - the core logic behind DailyTaskReminderBackgroundService in Orbit.Api, kept
/// independent of ASP.NET Core hosting so it can be unit tested directly. Unlike
/// <see cref="Orbit.Core.Tasks.OverdueNotifications.OverdueTaskNotificationScheduler"/>, a task item is
/// eligible again every day it stays incomplete, rather than only once.
/// </summary>
public sealed class DailyTaskReminderScheduler
{
    private readonly IDailyTaskReminderRepository _dailyTaskReminderRepository;

    public DailyTaskReminderScheduler(IDailyTaskReminderRepository dailyTaskReminderRepository)
    {
        _dailyTaskReminderRepository = dailyTaskReminderRepository;
    }

    /// <summary>
    /// A reminder is due once its configured time of day has been reached on <paramref name="nowLocal"/>'s
    /// calendar date, and hasn't been due for longer than <paramref name="lookBackWindow"/> - the window
    /// bounds how late a missed reminder can still fire (e.g. after the app was briefly down), rather than
    /// silently sending it hours late the first time this runs after a longer outage.
    /// <paramref name="maxResults"/> caps how many reminders a single call returns, protecting against a
    /// burst of simultaneously due reminders (e.g. many tasks all set to remind at midnight) overwhelming
    /// the caller - anything beyond the cap is simply picked up by the next call instead of being dropped.
    /// </summary>
    public async Task<IReadOnlyList<DueDailyTaskReminder>> FindDueRemindersAsync(
        DateTimeOffset nowLocal, TimeSpan lookBackWindow, CancellationToken cancellationToken, int maxResults = int.MaxValue)
    {
        var candidates = await _dailyTaskReminderRepository.GetEligibleAsync(cancellationToken);
        var today = DateOnly.FromDateTime(nowLocal.DateTime);
        var dueReminders = new List<DueDailyTaskReminder>();

        foreach (var candidate in candidates)
        {
            if (dueReminders.Count >= maxResults)
            {
                break;
            }

            if (!IsDue(candidate.TimeOfDay, today, nowLocal, lookBackWindow))
            {
                continue;
            }

            if (await _dailyTaskReminderRepository.HasBeenSentAsync(candidate.TaskItemId, today, cancellationToken))
            {
                continue;
            }

            dueReminders.Add(new DueDailyTaskReminder(
                candidate.TaskItemId, candidate.TaskListId, candidate.UserId, candidate.TaskListTitle, candidate.Description,
                candidate.DueDateUtc, candidate.NotificationChannel, today));
        }

        return dueReminders;
    }

    private static bool IsDue(TimeOnly timeOfDay, DateOnly today, DateTimeOffset nowLocal, TimeSpan lookBackWindow)
    {
        var reminderAt = today.ToDateTime(timeOfDay);
        var nowLocalDateTime = nowLocal.DateTime;
        return reminderAt <= nowLocalDateTime && reminderAt >= nowLocalDateTime - lookBackWindow;
    }
}
