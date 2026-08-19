namespace Orbit.Core.Tasks.OverdueNotifications;

/// <summary>
/// Finds task items that have just become overdue and haven't been notified about yet - the core logic
/// behind OverdueTaskNotificationBackgroundService in Orbit.Api, kept independent of ASP.NET Core
/// hosting so it can be unit tested directly. Unlike <see cref="Orbit.Core.Calendar.Reminders.EventReminderScheduler"/>,
/// there is no look-back window: "overdue and not yet notified" is a durable state (not a point in time
/// that can be missed by a brief outage), so a task item stays eligible for exactly one notification for
/// as long as it remains overdue and unnotified.
/// </summary>
public sealed class OverdueTaskNotificationScheduler
{
    private readonly IOverdueTaskNotificationRepository _overdueTaskNotificationRepository;

    public OverdueTaskNotificationScheduler(IOverdueTaskNotificationRepository overdueTaskNotificationRepository)
    {
        _overdueTaskNotificationRepository = overdueTaskNotificationRepository;
    }

    /// <summary>
    /// <paramref name="maxResults"/> caps how many overdue task items a single call returns, protecting
    /// against a burst of simultaneously overdue items overwhelming the caller - anything beyond the cap
    /// is simply picked up by the next call instead of being dropped.
    /// </summary>
    public async Task<IReadOnlyList<OverdueTaskItem>> FindNewlyOverdueAsync(
        DateTimeOffset nowUtc, CancellationToken cancellationToken, int maxResults = int.MaxValue)
    {
        var candidates = await _overdueTaskNotificationRepository.GetIncompleteWithDueDateAsync(cancellationToken);
        var newlyOverdue = new List<OverdueTaskItem>();

        foreach (var candidate in candidates.Where(candidate => candidate.DueDateUtc <= nowUtc))
        {
            if (newlyOverdue.Count >= maxResults)
            {
                break;
            }

            if (await _overdueTaskNotificationRepository.HasBeenNotifiedAsync(candidate.TaskItemId, cancellationToken))
            {
                continue;
            }

            newlyOverdue.Add(candidate);
        }

        return newlyOverdue;
    }
}
