namespace Orbit.Contracts.Notifications;

public sealed record NotificationEntryDto(
    Guid Id, string Kind, string Title, string Body, string? Url, DateTimeOffset CreatedAtUtc, bool IsRead);
