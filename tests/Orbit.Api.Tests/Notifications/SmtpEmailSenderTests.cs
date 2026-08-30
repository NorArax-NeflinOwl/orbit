using Orbit.Api.Notifications;
using Orbit.Api.Tests.TestDoubles;
using Xunit;

namespace Orbit.Api.Tests.Notifications;

/// <summary>
/// Covers the sender against an SMTP server that only exists for the duration of the test. The gap this
/// closes was never MailKit's protocol work - it was that nothing had ever checked Orbit's own
/// decisions around it: that an unconfigured deployment stays quiet and says so, that half-configured
/// credentials are treated as no configuration at all, and that what reaches the wire is the message
/// that was asked for.
/// </summary>
public sealed class SmtpEmailSenderTests
{
    [Fact]
    public async Task A_configured_sender_delivers_the_message_it_was_given()
    {
        await using var server = FakeSmtpServer.Start();
        var sender = CreateSender(ConfiguredSettings(server.Port));

        await sender.SendAsync("someone@example.test", "Your event starts soon", "Ten minutes to go.", CancellationToken.None);
        await server.WaitForConversationAsync();

        Assert.Contains(server.ReceivedLines, line => line.Contains("MAIL FROM:<orbit@example.test>"));
        Assert.Contains(server.ReceivedLines, line => line.Contains("RCPT TO:<someone@example.test>"));
        Assert.Contains("Your event starts soon", server.Transcript);
        Assert.Contains("Ten minutes to go.", server.Transcript);
    }

    [Fact]
    public async Task The_sender_signs_in_before_sending()
    {
        await using var server = FakeSmtpServer.Start();
        var sender = CreateSender(ConfiguredSettings(server.Port));

        await sender.SendAsync("someone@example.test", "Subject", "Body", CancellationToken.None);
        await server.WaitForConversationAsync();

        // Nearly every real SMTP relay refuses an unauthenticated MAIL FROM, so skipping this would mean
        // every reminder silently failing against anything but a local test server.
        var authIndex = server.ReceivedLines.FindIndex(line => line.StartsWith("AUTH", StringComparison.Ordinal));
        var mailFromIndex = server.ReceivedLines.FindIndex(line => line.StartsWith("MAIL FROM", StringComparison.Ordinal));
        Assert.InRange(authIndex, 0, mailFromIndex - 1);
    }

    [Fact]
    public async Task An_unconfigured_deployment_sends_nothing_and_says_so()
    {
        var logger = new RecordingLogger<SmtpEmailSender>();
        var sender = new SmtpEmailSender(new TestOptionsMonitor<SmtpSettings>(new SmtpSettings()), logger);

        // No connection is attempted at all, which is what lets a fresh checkout run with no mail server
        // anywhere near it - see SmtpSettings.IsConfigured.
        await sender.SendAsync("someone@example.test", "Subject", "Body", CancellationToken.None);

        Assert.Contains(logger.Entries, entry => entry.Message.Contains("Smtp is not configured"));
    }

    [Fact]
    public async Task A_host_with_no_credentials_counts_as_unconfigured_rather_than_as_something_to_try()
    {
        await using var server = FakeSmtpServer.Start();
        var settings = ConfiguredSettings(server.Port);
        settings.Password = null;
        var logger = new RecordingLogger<SmtpEmailSender>();

        await new SmtpEmailSender(new TestOptionsMonitor<SmtpSettings>(settings), logger)
            .SendAsync("someone@example.test", "Subject", "Body", CancellationToken.None);

        // Connecting and then failing to authenticate would surface as an exception from a background
        // service, on a schedule, for a deployment that had simply not finished being set up.
        Assert.Empty(server.ReceivedLines);
        Assert.Contains(logger.Entries, entry => entry.Message.Contains("Smtp:UserName/Smtp:Password"));
    }

    private static SmtpEmailSender CreateSender(SmtpSettings settings)
        => new(new TestOptionsMonitor<SmtpSettings>(settings), new RecordingLogger<SmtpEmailSender>());

    /// <summary>
    /// Points at the loopback server. UseStartTls is off deliberately: FakeSmtpServer advertises no
    /// STARTTLS, and asking for it would be asking for a certificate the test would then have to trust.
    /// </summary>
    private static SmtpSettings ConfiguredSettings(int port)
        => new()
        {
            Host = "127.0.0.1",
            Port = port,
            UserName = "orbit",
            Password = "not-a-real-password",
            UseStartTls = false,
            FromAddress = "orbit@example.test",
            FromDisplayName = "Orbit"
        };
}
