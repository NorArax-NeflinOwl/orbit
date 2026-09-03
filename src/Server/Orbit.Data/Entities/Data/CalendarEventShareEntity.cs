namespace Orbit.Data.Entities;

/// <summary>
/// Persistence shape of <see cref="Orbit.Core.Calendar.CalendarEventShare"/>, mapped separately so
/// schema changes don't force changes onto domain logic, and vice versa.
/// </summary>
public sealed class CalendarEventShareEntity
{
    public Guid Id { get; set; }
    public Guid SourceCalendarEventId { get; set; }
    public Guid OwnerUserId { get; set; }
    public Guid RecipientUserId { get; set; }
    public string AccessLevel { get; set; } = "ReadOnly";
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? AcceptedAtUtc { get; set; }
}
