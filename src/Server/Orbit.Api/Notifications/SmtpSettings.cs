namespace Orbit.Api.Notifications;

/// <summary>
/// SMTP connection details for outgoing email - currently just calendar event reminders (see
/// CalendarEventReminderBackgroundService). Bound from the "Smtp" configuration section;
/// <see cref="Password"/> in particular must come from an environment variable or user-secrets, never
/// from a committed appsettings file (see .env.example).
/// </summary>
public sealed class SmtpSettings
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public bool UseStartTls { get; set; } = true;
    public string FromAddress { get; set; } = string.Empty;
    public string FromDisplayName { get; set; } = "Orbit";

    /// <summary>
    /// False when no SMTP server has been set up yet - SmtpEmailSender then skips sending (logging a
    /// warning instead) rather than failing outright, so a fresh local checkout still runs without
    /// anyone having configured email delivery.
    /// </summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(FromAddress);
}
