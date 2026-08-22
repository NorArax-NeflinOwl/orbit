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

    [ClientAction(ClientActionCategory.Save)]
    private sealed record TaggedRequest : IRequest<string?>;

    private sealed record PlainRequest : IRequest<string?>;
}
