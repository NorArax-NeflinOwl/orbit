namespace Orbit.Api.Notifications;

/// <summary>
/// VAPID (RFC 8292) key pair Orbit.Api signs outgoing push notifications with, and identifies itself to
/// push services by. Bound from the "Vapid" configuration section; <see cref="PrivateKeyBase64Url"/> in
/// particular must come from an environment variable, never from a committed appsettings file (see
/// .env.example). Generate a key pair once with, e.g., the `web-push generate-vapid-keys` npm CLI, or
/// any other RFC 8292-compliant tool - both keys are P-256 (raw, base64url-encoded).
/// </summary>
public sealed class VapidSettings
{
    public string PublicKeyBase64Url { get; set; } = string.Empty;
    public string PrivateKeyBase64Url { get; set; } = string.Empty;

    /// <summary>
    /// A "mailto:" address or HTTPS URL identifying who is sending the push notification - required by
    /// RFC 8292 so a push service operator has a way to contact the sender about abuse.
    /// </summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// False until a VAPID key pair has been configured - VapidPushNotificationSender then skips sending
    /// (logging a warning instead) rather than failing outright, so a fresh local checkout still runs
    /// without anyone having generated keys (see SmtpSettings.IsConfigured for the same reasoning applied
    /// to calendar event reminder emails).
    /// </summary>
    public bool IsConfigured
        => !string.IsNullOrWhiteSpace(PublicKeyBase64Url) && !string.IsNullOrWhiteSpace(PrivateKeyBase64Url)
            && !string.IsNullOrWhiteSpace(Subject);
}
