namespace Orbit.Core.Notifications;

/// <summary>
/// Sends a single transactional email. Implemented against a real SMTP server outside Orbit.Core (see
/// SmtpEmailSender in Orbit.Api), so domain and application logic never depends on a specific mail
/// library or transport.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(string toEmailAddress, string subject, string body, CancellationToken cancellationToken);
}
