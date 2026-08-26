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

    /// <summary>
    /// The share already offered for sourceCalendarEventId to recipientUserId, if one exists - accepted
    /// or still pending, either way counts as "already shared" for ShareCalendarEventCommandHandler's
    /// duplicate check, so it re-sends the existing offer as a reminder instead of creating a second one.
    /// </summary>
    Task<CalendarEventShare?> FindExistingAsync(Guid sourceCalendarEventId, Guid recipientUserId, CancellationToken cancellationToken);

    /// <summary>The *accepted* grant for sourceCalendarEventId to recipientUserId, if one exists - see CalendarEventAccessResolver.</summary>
    Task<CalendarEventShare?> FindAcceptedGrantAsync(Guid sourceCalendarEventId, Guid recipientUserId, CancellationToken cancellationToken);

    /// <summary>Every event recipientUserId has accepted access to, regardless of which owner shared it - see CalendarEventAccessResolver.ResolveAllAsync.</summary>
    Task<IReadOnlyList<CalendarEventShare>> GetAcceptedGrantsForRecipientAsync(Guid recipientUserId, CancellationToken cancellationToken);

    /// <summary>
    /// Which of ownerUserId's own calendar events somebody else currently holds accepted access to - the owner's
    /// side of the relationship, which nothing else exposes. Mirrors INoteShareRepository's method of the
    /// same shape, and exists for the same reason: a mobile client cannot hold an edit lock, so anything
    /// another person can change is read-only while offline (info/orbit-maui-plan.md §5.4).
    ///
    /// A whole set in one query, because the caller asks it of every calendar event in a list.
    /// </summary>
    Task<IReadOnlySet<Guid>> GetSharedOutCalendarEventIdsAsync(Guid ownerUserId, CancellationToken cancellationToken);
}
