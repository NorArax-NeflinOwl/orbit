using Microsoft.Extensions.Logging;
using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Abstractions;
using Xunit;

namespace Orbit.Api.Tests.Abstractions;

public sealed class LoggingDispatcherTests
{
    [Fact]
    public async Task SendAsync_tags_a_client_actions_started_and_completed_log_lines_with_its_category()
    {
        var logger = new RecordingLogger<LoggingDispatcher>();
        var dispatcher = new LoggingDispatcher(StubDispatcher.ReturningDefault(), logger);

        await dispatcher.SendAsync(new TaggedRequest());

        Assert.Equal(2, logger.Entries.Count);
        Assert.All(logger.Entries, entry => Assert.StartsWith("[ACTION:Save] TaggedRequest", entry.Message));
    }

    [Fact]
    public async Task SendAsync_leaves_a_plain_requests_log_lines_unchanged()
    {
        var logger = new RecordingLogger<LoggingDispatcher>();
        var dispatcher = new LoggingDispatcher(StubDispatcher.ReturningDefault(), logger);

        await dispatcher.SendAsync(new PlainRequest());

        Assert.Equal(2, logger.Entries.Count);
        Assert.All(logger.Entries, entry => Assert.DoesNotContain("[ACTION:", entry.Message));
    }

    [Fact]
    public async Task SendAsync_tags_the_error_log_line_when_a_client_action_request_fails()
    {
        var logger = new RecordingLogger<LoggingDispatcher>();
        var failure = new InvalidOperationException("boom");
        var dispatcher = new LoggingDispatcher(StubDispatcher.Throwing(failure), logger);

        await Assert.ThrowsAsync<InvalidOperationException>(() => dispatcher.SendAsync(new TaggedRequest()));

        var errorEntry = Assert.Single(logger.Entries, entry => entry.Level == LogLevel.Error);
        Assert.StartsWith("[ACTION:Save] TaggedRequest", errorEntry.Message);
        Assert.Same(failure, errorEntry.Exception);
    }

    [Fact]
    public async Task SendAsync_logs_a_refused_request_as_a_warning_rather_than_an_error()
    {
        var logger = new RecordingLogger<LoggingDispatcher>();
        var refusal = new InvalidRequestException("A private note can't be shared.");
        var dispatcher = new LoggingDispatcher(StubDispatcher.Throwing(refusal), logger);

        await Assert.ThrowsAsync<InvalidRequestException>(() => dispatcher.SendAsync(new TaggedRequest()));

        // The caller being told no is expected input the API answers with a 400, not a fault. Left at
        // Error, the Error level fills with ordinary refusals and stops meaning anything.
        var entry = Assert.Single(logger.Entries, candidate => candidate.Level == LogLevel.Warning);
        Assert.StartsWith("[ACTION:Save] TaggedRequest failed", entry.Message);
        Assert.Same(refusal, entry.Exception);
        Assert.DoesNotContain(logger.Entries, candidate => candidate.Level == LogLevel.Error);
    }

    [Fact]
    public async Task SendAsync_still_logs_a_plain_requests_refusal_as_a_warning()
    {
        var logger = new RecordingLogger<LoggingDispatcher>();
        var dispatcher = new LoggingDispatcher(
            StubDispatcher.Throwing(new InvalidRequestException("This link would create a cycle.")), logger);

        await Assert.ThrowsAsync<InvalidRequestException>(() => dispatcher.SendAsync(new PlainRequest()));

        Assert.Single(logger.Entries, candidate => candidate.Level == LogLevel.Warning);
        Assert.DoesNotContain(logger.Entries, candidate => candidate.Level == LogLevel.Error);
    }

    [Fact]
    public async Task SendAsync_still_logs_anything_else_as_an_error()
    {
        // The control: the whole point is that a real fault stays a real fault.
        var logger = new RecordingLogger<LoggingDispatcher>();
        var dispatcher = new LoggingDispatcher(StubDispatcher.Throwing(new TimeoutException("gone")), logger);

        await Assert.ThrowsAsync<TimeoutException>(() => dispatcher.SendAsync(new TaggedRequest()));

        Assert.Single(logger.Entries, candidate => candidate.Level == LogLevel.Error);
        Assert.DoesNotContain(logger.Entries, candidate => candidate.Level == LogLevel.Warning);
    }

    [ClientAction(ClientActionCategory.Save)]
    private sealed record TaggedRequest : IRequest<string?>;

    private sealed record PlainRequest : IRequest<string?>;
}
