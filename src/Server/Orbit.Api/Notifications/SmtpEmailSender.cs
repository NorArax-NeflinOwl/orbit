using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using Orbit.Core.Notifications;

namespace Orbit.Api.Notifications;

/// <summary>
/// Sends email through a real SMTP server via MailKit. Logs a warning and does nothing when
/// <see cref="SmtpSettings"/> isn't configured, rather than throwing - see that class's comment for
/// why.
/// </summary>
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly IOptionsMonitor<SmtpSettings> _settings;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptionsMonitor<SmtpSettings> settings, ILogger<SmtpEmailSender> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public async Task SendAsync(string toEmailAddress, string subject, string body, CancellationToken cancellationToken)
    {
        var currentSettings = _settings.CurrentValue;
        if (!currentSettings.IsConfigured)
        {
            _logger.LogWarning(
                "Smtp is not configured (see Smtp:Host/Smtp:FromAddress) - dropped an email to {ToEmailAddress}: {Subject}",
                toEmailAddress, subject);
            return;
        }
        if(string.IsNullOrEmpty(currentSettings.UserName) || string.IsNullOrEmpty(currentSettings.Password))
        {
            _logger.LogWarning(
                "Smtp is not configured (see Smtp:UserName/Smtp:Password) - dropped an email to {ToEmailAddress}: {Subject}",
                toEmailAddress, subject);
            return;
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(currentSettings.FromDisplayName, currentSettings.FromAddress));
        message.To.Add(MailboxAddress.Parse(toEmailAddress));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        using var smtpClient = new SmtpClient();

        // Some networks (VPNs, corporate/home firewalls) block the OCSP/CRL lookups .NET performs to
        // check whether the server's certificate has been revoked, which SslStream then treats as a
        // hard TLS failure ("An incomplete certificate revocation check occurred") even though the
        // certificate itself is valid. Revocation checking still happens whenever it can complete; this
        // only stops an unrelated network hiccup from blocking every outgoing email.
        smtpClient.CheckCertificateRevocation = false;

        var socketOptions = currentSettings.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto;
        await smtpClient.ConnectAsync(currentSettings.Host, currentSettings.Port, socketOptions, cancellationToken);

        if (!string.IsNullOrWhiteSpace(currentSettings.UserName))
        {
            await smtpClient.AuthenticateAsync(currentSettings.UserName, currentSettings.Password, cancellationToken);
        }

        await smtpClient.SendAsync(message, cancellationToken);
        await smtpClient.DisconnectAsync(quit: true, cancellationToken);
    }
}
