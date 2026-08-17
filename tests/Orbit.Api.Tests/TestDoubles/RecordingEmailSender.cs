using Orbit.Core.Notifications;

namespace Orbit.Api.Tests.TestDoubles;

/// <summary>
/// In-memory <see cref="IEmailSender"/> stub that records every call instead of sending anything, so
/// tests can assert on what would have been sent (or that nothing was).
/// </summary>
internal sealed class RecordingEmailSender : IEmailSender
{
    private readonly List<SentEmail> _sentEmails = [];

    public IReadOnlyList<SentEmail> SentEmails => _sentEmails;

    public Task SendAsync(string toEmailAddress, string subject, string body, CancellationToken cancellationToken)
    {
        _sentEmails.Add(new SentEmail(toEmailAddress, subject, body));
        return Task.CompletedTask;
    }

    internal sealed record SentEmail(string ToEmailAddress, string Subject, string Body);
}
