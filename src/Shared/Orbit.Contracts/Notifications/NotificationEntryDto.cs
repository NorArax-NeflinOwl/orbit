namespace Orbit.Contracts.Notifications;

/// <param name="IsDismissed">
/// True once the reader has cleared this entry out of the panel. The notifications page still lists it,
/// marked as cleared, until the retention window deletes it - see NotificationEntry.Dismiss.
/// </param>
public sealed record NotificationEntryDto(
    Guid Id, string Kind, string Title, string Body, string? Url, DateTimeOffset CreatedAtUtc, bool IsRead,
    bool IsDismissed = false);
