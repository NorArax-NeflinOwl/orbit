namespace Orbit.Api.Notifications;

/// <summary>
/// How Orbit.Api reaches Firebase Cloud Messaging to notify the Orbit.Maui apps. Bound from the
/// "Firebase" configuration section.
///
/// The service account key is a real private key granting admin access to the Firebase project, so
/// only its *path* lives in configuration - never the key itself in a committed file. See secrets/README.md.
/// Left unset, FirebasePushNotificationSender logs a warning and skips sending, exactly as
/// VapidSettings does for web push, so a fresh checkout still runs without Firebase set up.
/// </summary>
public sealed class FirebaseSettings
{
    public const string SectionName = "Firebase";

    /// <summary>Absolute path to the service account JSON downloaded from the Firebase console.</summary>
    public string ServiceAccountKeyPath { get; set; } = string.Empty;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ServiceAccountKeyPath) && File.Exists(ServiceAccountKeyPath);
}
