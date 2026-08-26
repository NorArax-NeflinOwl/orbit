namespace Orbit.Core.Notifications;

/// <summary>
/// One destination a user has approved push notifications on - a browser or a phone. A user can hold
/// several at once (one per browser or device); <see cref="PushNotificationDispatcher"/> is what fans a
/// single notification out to all of them.
///
/// <see cref="Transport"/> says which of the two registrations below is filled in, because a browser
/// and an app are reached in entirely different ways: Web Push POSTs to a URL the browser handed out,
/// while a mobile app is a token Firebase resolves. Rather than a row of nullable strings whose valid
/// combinations are anyone's guess, each shape is its own value and the factories are the only way in.
/// </summary>
public sealed class PushSubscription
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public PushTransport Transport { get; private set; }

    /// <summary>Set when <see cref="Transport"/> is <see cref="PushTransport.WebPush"/>, null otherwise.</summary>
    public WebPushRegistration? WebPush { get; private set; }

    /// <summary>Set when <see cref="Transport"/> is <see cref="PushTransport.Firebase"/>, null otherwise.</summary>
    public DeviceRegistration? Device { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    private PushSubscription(
        Guid id, Guid userId, PushTransport transport, WebPushRegistration? webPush, DeviceRegistration? device,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        UserId = userId;
        Transport = transport;
        WebPush = webPush;
        Device = device;
        CreatedAtUtc = createdAtUtc;
    }

    public static PushSubscription CreateForBrowser(Guid userId, WebPushRegistration registration)
        => new(Guid.NewGuid(), userId, PushTransport.WebPush, registration, device: null, DateTimeOffset.UtcNow);

    public static PushSubscription CreateForDevice(Guid userId, DeviceRegistration registration)
        => new(Guid.NewGuid(), userId, PushTransport.Firebase, webPush: null, registration, DateTimeOffset.UtcNow);

    /// <summary>Rebuilds a subscription from already-persisted values, bypassing creation rules.</summary>
    public static PushSubscription FromPersistence(
        Guid id, Guid userId, PushTransport transport, WebPushRegistration? webPush, DeviceRegistration? device,
        DateTimeOffset createdAtUtc)
        => new(id, userId, transport, webPush, device, createdAtUtc);
}
