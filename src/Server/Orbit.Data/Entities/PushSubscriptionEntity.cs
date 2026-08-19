namespace Orbit.Data.Entities;

/// <summary>
/// Persistence shape of a browser's Web Push registration, mapped separately from
/// <see cref="Orbit.Core.Notifications.PushSubscription"/> so schema changes don't force changes onto
/// domain logic, and vice versa.
/// </summary>
public sealed class PushSubscriptionEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Endpoint { get; set; } = string.Empty;
    public string P256dhBase64 { get; set; } = string.Empty;
    public string AuthBase64 { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
}
