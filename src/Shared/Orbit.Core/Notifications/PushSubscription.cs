namespace Orbit.Core.Notifications;

/// <summary>
/// A single browser's Web Push registration for one user - everything <see cref="IPushNotificationSender"/>
/// needs to deliver a message to it (see RFC 8030/RFC 8291). A user can hold more than one of these at
/// once (e.g. one per browser or device that approved push notifications); <see cref="PushNotificationDispatcher"/>
/// is what fans a single notification out to all of them.
/// </summary>
public sealed class PushSubscription
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }

    /// <summary>The push service URL this subscription's messages must be POSTed to.</summary>
    public string Endpoint { get; private set; }

    /// <summary>The subscriber's P-256 ECDH public key (raw, base64) - see RFC 8291's message encryption.</summary>
    public string P256dhBase64 { get; private set; }

    /// <summary>The subscriber's 16-byte authentication secret (base64) - see RFC 8291's message encryption.</summary>
    public string AuthBase64 { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    private PushSubscription(
        Guid id, Guid userId, string endpoint, string p256dhBase64, string authBase64, DateTimeOffset createdAtUtc)
    {
        Id = id;
        UserId = userId;
        Endpoint = endpoint;
        P256dhBase64 = p256dhBase64;
        AuthBase64 = authBase64;
        CreatedAtUtc = createdAtUtc;
    }

    public static PushSubscription Create(Guid userId, string endpoint, string p256dhBase64, string authBase64)
        => new(Guid.NewGuid(), userId, endpoint, p256dhBase64, authBase64, DateTimeOffset.UtcNow);

    /// <summary>
    /// Rebuilds a push subscription from already-persisted values, bypassing creation rules.
    /// </summary>
    public static PushSubscription FromPersistence(
        Guid id, Guid userId, string endpoint, string p256dhBase64, string authBase64, DateTimeOffset createdAtUtc)
        => new(id, userId, endpoint, p256dhBase64, authBase64, createdAtUtc);
}
