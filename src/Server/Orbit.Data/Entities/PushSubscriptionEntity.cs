namespace Orbit.Data.Entities;

/// <summary>
/// Persistence shape of one push destination - a browser or a phone - mapped separately from
/// <see cref="Orbit.Core.Notifications.PushSubscription"/> so schema changes don't force changes onto
/// domain logic, and vice versa.
///
/// The Web Push and device columns are mutually exclusive: Transport says which set is filled in, and
/// the other is null. Two nullable groups in one table rather than two tables, because everything that
/// reads these wants "every destination for this user" regardless of how they are reached.
/// </summary>
public sealed class PushSubscriptionEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    /// <summary>Stored by name - see Orbit.Core.Notifications.PushTransport.</summary>
    public string Transport { get; set; } = string.Empty;

    public string? Endpoint { get; set; }
    public string? P256dhBase64 { get; set; }
    public string? AuthBase64 { get; set; }

    /// <summary>The FCM registration token, for a mobile subscription.</summary>
    public string? DeviceToken { get; set; }

    /// <summary>Stored by name - see Orbit.Core.Mobile.MobilePlatform.</summary>
    public string? DevicePlatform { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
