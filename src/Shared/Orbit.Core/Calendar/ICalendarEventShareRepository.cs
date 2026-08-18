namespace Orbit.Core.Calendar;

public interface ICalendarEventShareRepository
{
    Task AddAsync(CalendarEventShare share, CancellationToken cancellationToken);

    /// <summary>
    /// Scoped to recipientUserId, the same way ICalendarEventRepository.GetByIdAsync is scoped to an
    /// owner - returns null both when the share doesn't exist and when it exists but was offered to
    /// someone else, so a caller can't tell one case from the other by probing ids.
    /// </summary>
    Task<CalendarEventShare?> GetByIdAsync(Guid recipientUserId, Guid id, CancellationToken cancellationToken);

    Task UpdateAsync(CalendarEventShare share, CancellationToken cancellationToken);

    /// <summary>
    /// User ids of every recipient who has accepted a share of sourceCalendarEventId - used to decide
    /// which guests, in addition to the owner, should receive the event's reminder e-mails (see
    /// CalendarEventReminderBackgroundService). A guest who was invited but never accepted the share is
    /// not included.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetAcceptedRecipientUserIdsAsync(Guid sourceCalendarEventId, CancellationToken cancellationToken);
}
