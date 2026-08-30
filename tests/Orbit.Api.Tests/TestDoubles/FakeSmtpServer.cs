using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Orbit.Api.Tests.TestDoubles;

/// <summary>
/// A single-conversation SMTP server on a loopback port, speaking just enough of RFC 5321 for MailKit
/// to hand it a message: greeting, EHLO, AUTH PLAIN, the envelope, DATA, QUIT.
///
/// This exists because <see cref="Orbit.Api.Notifications.SmtpEmailSender"/> constructs its MailKit
/// client itself, so there is nothing to substitute - the only seam left is the socket. It deliberately
/// offers no STARTTLS: MailKit's Auto option upgrades when the server advertises it, and staying
/// unencrypted keeps the test free of a certificate nobody would trust anyway.
/// </summary>
internal sealed class FakeSmtpServer : IAsyncDisposable
{
    private readonly TcpListener _listener;
    private readonly Task _conversation;
    private readonly CancellationTokenSource _stopping = new();

    private FakeSmtpServer(TcpListener listener)
    {
        _listener = listener;
        _conversation = HoldOneConversationAsync(_stopping.Token);
    }

    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    /// <summary>Every line the client sent, envelope and message body alike, in the order it sent them.</summary>
    public List<string> ReceivedLines { get; } = [];

    /// <summary>Everything the client sent, so a test can assert on the message as one piece of text.</summary>
    public string Transcript => string.Join("\n", ReceivedLines);

    public static FakeSmtpServer Start()
    {
        var listener = new TcpListener(IPAddress.Loopback, port: 0);
        listener.Start();
        return new FakeSmtpServer(listener);
    }

    /// <summary>
    /// Waits for the client to finish, so a test asserts on a complete transcript rather than on
    /// whatever had arrived by the time it looked.
    /// </summary>
    public Task WaitForConversationAsync() => _conversation;

    private async Task HoldOneConversationAsync(CancellationToken cancellationToken)
    {
        using var client = await _listener.AcceptTcpClientAsync(cancellationToken);
        await using var stream = client.GetStream();
        // No byte-order mark: SMTP is a line protocol, and a BOM ahead of the greeting is three bytes
        // MailKit reads as the start of a status code it cannot parse.
        var protocolEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        using var reader = new StreamReader(stream, protocolEncoding);
        await using var writer = new StreamWriter(stream, protocolEncoding) { AutoFlush = true, NewLine = "\r\n" };

        await writer.WriteLineAsync("220 localhost ESMTP FakeSmtpServer");

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            ReceivedLines.Add(line);
            var reply = ReplyTo(line);
            if (reply is not null)
            {
                await writer.WriteLineAsync(reply);
            }

            if (line.StartsWith("QUIT", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (line.StartsWith("DATA", StringComparison.OrdinalIgnoreCase))
            {
                await ReadMessageBodyAsync(reader, writer, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Null for a line that needs no answer - the credentials MailKit sends on the line after AUTH,
    /// which are already acknowledged by the 235 that follows the AUTH command itself.
    /// </summary>
    private static string? ReplyTo(string line)
    {
        if (line.StartsWith("EHLO", StringComparison.OrdinalIgnoreCase))
        {
            // Only PLAIN, so which mechanism MailKit picks is not left to chance.
            return "250-localhost\r\n250 AUTH PLAIN";
        }

        return line switch
        {
            _ when line.StartsWith("HELO", StringComparison.OrdinalIgnoreCase) => "250 localhost",
            _ when line.StartsWith("AUTH", StringComparison.OrdinalIgnoreCase) => "235 2.7.0 Authenticated",
            _ when line.StartsWith("MAIL FROM", StringComparison.OrdinalIgnoreCase) => "250 2.1.0 Ok",
            _ when line.StartsWith("RCPT TO", StringComparison.OrdinalIgnoreCase) => "250 2.1.5 Ok",
            _ when line.StartsWith("DATA", StringComparison.OrdinalIgnoreCase) => "354 End data with <CR><LF>.<CR><LF>",
            _ when line.StartsWith("QUIT", StringComparison.OrdinalIgnoreCase) => "221 2.0.0 Bye",
            _ => null
        };
    }

    private async Task ReadMessageBodyAsync(StreamReader reader, StreamWriter writer, CancellationToken cancellationToken)
    {
        while (await reader.ReadLineAsync(cancellationToken) is { } line && line != ".")
        {
            ReceivedLines.Add(line);
        }

        await writer.WriteLineAsync("250 2.0.0 Ok: queued");
    }

    public async ValueTask DisposeAsync()
    {
        await _stopping.CancelAsync();
        _listener.Stop();
        _stopping.Dispose();

        try
        {
            await _conversation;
        }
        catch (OperationCanceledException)
        {
            // Expected when a test finishes without the client ever connecting.
        }
        catch (SocketException)
        {
            // The listener was stopped out from under an accept that had not completed.
        }
    }
}
