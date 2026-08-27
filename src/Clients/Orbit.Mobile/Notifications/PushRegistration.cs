using Microsoft.Extensions.Logging;
using Orbit.Mobile.Api;

namespace Orbit.Mobile.Notifications;

/// <summary>
/// Tells Orbit how to reach this device, once the device agrees to be reached.
///
/// Run on every sign-in rather than once ever. A push token is not permanent - reinstalling the app or
/// clearing its data mints a new one, and the old one silently stops working - so the registration is
/// something to keep current rather than a one-off setup step.
///
/// Nothing here fails loudly. Push is an addition to the in-app notification feed, not a replacement
/// for it: a reader who declined, or whose registration did not go through, still sees everything when
/// they open the app. Blocking a sign-in over it would trade the whole app for one of its conveniences.
/// </summary>
public sealed class PushRegistration
{
    private readonly IDevicePushNotifications _devicePushNotifications;
    private readonly NotificationsClient _notificationsClient;
    private readonly ILogger<PushRegistration> _logger;

    public PushRegistration(
        IDevicePushNotifications devicePushNotifications, NotificationsClient notificationsClient,
        ILogger<PushRegistration> logger)
    {
        _devicePushNotifications = devicePushNotifications;
        _notificationsClient = notificationsClient;
        _logger = logger;
    }

    /// <summary>
    /// True when Orbit now holds a token for this device. The caller is free to ignore it - it is worth
    /// returning only so a diagnostics screen can say why push is quiet.
    /// </summary>
    public async Task<bool> RegisterThisDeviceAsync(CancellationToken cancellationToken = default)
    {
        var registration = await ReadTokenAsync(cancellationToken);
        if (registration is not { Outcome: PushRegistrationOutcome.Registered, DeviceToken: { Length: > 0 } token })
        {
            LogWhyNot(registration?.Outcome);
            return false;
        }

        try
        {
            await _notificationsClient.RegisterDeviceAsync(
                token, _devicePushNotifications.Platform, cancellationToken);
            return true;
        }
        catch (HttpRequestException exception)
        {
            // Offline at sign-in is ordinary, and the next sign-in registers again.
            _logger.LogInformation(exception, "Could not register this device for push notifications");
            return false;
        }
    }

    /// <summary>
    /// A platform call can throw for reasons that have nothing to do with the reader - an entitlement
    /// this build does not carry, a simulator without a push capability. None of them is worth taking
    /// the sign-in down with.
    /// </summary>
    private async Task<PushRegistrationResult?> ReadTokenAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _devicePushNotifications.RegisterAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "The device could not be registered for push notifications");
            return null;
        }
    }

    private void LogWhyNot(PushRegistrationOutcome? outcome)
    {
        if (outcome == PushRegistrationOutcome.NotPermitted)
        {
            _logger.LogInformation("Push notifications were declined on this device");
            return;
        }

        if (outcome == PushRegistrationOutcome.NotAvailableHere)
        {
            _logger.LogInformation("This build cannot obtain a push token - see IDevicePushNotifications");
        }
    }
}
