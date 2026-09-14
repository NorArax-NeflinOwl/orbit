using Orbit.Core.Tasks.OverdueNotifications;

namespace Orbit.Core.Tasks.DailyReminders;

/// <summary>
/// Finds task items whose "remind daily" time of day has been reached today and haven't been reminded
/// about yet today - the core logic behind DailyTaskReminderBackgroundService in Orbit.Api, kept
/// independent of ASP.NET Core hosting so it can be unit tested directly. Unlike
/// <see cref="OverdueTaskNotificationScheduler"/>, a task item is eligible again every day it stays
/// incomplete, rather than only once.
///
/// <b>An overdue notice speaks instead of today's reminder.</b> The two schedulers used to know nothing
/// about each other, so an entry that was both late and reminded daily said the same thing twice a
/// minute apart - reported by the user on 2026-09-12 and again on 2026-09-14, and settled by them: the
/// overdue notice replaces the daily reminder, because a late entry has a notification of its own and
/// nothing in Orbit should say one thing twice.
///
/// Which of the two gives way is decided here rather than there, and that is deliberate: this is the
/// scheduler that can give way without losing anything. The overdue notice is sent once ever, so the day
/// it goes out is the only day the two collide - <see cref="SpokenForByAnOverdueNoticeAsync"/> stands
/// this one down for exactly that day, and the daily reminder carries on the next. Standing the overdue
/// notice down instead would have been the other way to read "replaces", and it loses the one piece of
/// news the reader has not had yet.
/// </summary>
public sealed class DailyTaskReminderScheduler
{
    private readonly IDailyTaskReminderRepository _dailyTaskReminderRepository;
    private readonly IOverdueTaskNotificationRepository _overdueTaskNotificationRepository;

    public DailyTaskReminderScheduler(
        IDailyTaskReminderRepository dailyTaskReminderRepository,
        IOverdueTaskNotificationRepository overdueTaskNotificationRepository)
    {
        _dailyTaskReminderRepository = dailyTaskReminderRepository;
        _overdueTaskNotificationRepository = overdueTaskNotificationRepository;
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

            if (await SpokenForByAnOverdueNoticeAsync(candidate, nowLocal, cancellationToken))
            {
                continue;
            }

            dueReminders.Add(new DueDailyTaskReminder(
                candidate.TaskItemId, candidate.TaskListId, candidate.UserId, candidate.TaskListTitle, candidate.Description,
                candidate.DueDateUtc, candidate.NotificationChannel, today));
        }

        return dueReminders;
    }

    /// <summary>
    /// Whether an overdue notice is about to say this entry's piece, so today's reminder would be the
    /// second half of a pair.
    ///
    /// The same question <see cref="OverdueTaskNotificationScheduler.FindNewlyOverdueAsync"/> asks, in
    /// the same words: past its due date, and not notified about yet. Asked rather than "was one sent
    /// today", so it does not matter which of the two services polls first - the one that gives way is
    /// this one either way, and it gives way for the day the notice goes out rather than for good. The
    /// day after, the notice has been sent (it is sent once ever) and this answers false again.
    ///
    /// An entry with no due date can never be overdue and is never held back. Comparing a UTC due date
    /// against a local <paramref name="nowLocal"/> is comparing two instants, which is what
    /// DateTimeOffset compares - the offsets differ and the moment does not.
    /// </summary>
    private async Task<bool> SpokenForByAnOverdueNoticeAsync(
        DailyTaskReminderCandidate candidate, DateTimeOffset nowLocal, CancellationToken cancellationToken)
        => candidate.DueDateUtc is { } dueUtc
            && dueUtc <= nowLocal
            && !await _overdueTaskNotificationRepository.HasBeenNotifiedAsync(candidate.TaskItemId, cancellationToken);

    private static bool IsDue(TimeOnly timeOfDay, DateOnly today, DateTimeOffset nowLocal, TimeSpan lookBackWindow)
    {
        var reminderAt = today.ToDateTime(timeOfDay);
        var nowLocalDateTime = nowLocal.DateTime;
        return reminderAt <= nowLocalDateTime && reminderAt >= nowLocalDateTime - lookBackWindow;
    }
}
