using Orbit.Api.Sync;
using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Sync;
using Xunit;

namespace Orbit.Api.Tests.Sync;

/// <summary>
/// The cursor goes straight back into a URL, so how it is written matters as much as what it means.
/// This is here because the first version returned a DateTimeOffset, whose "+00:00" offset a client
/// pasting it into a query string turns into a space - and the server then cannot read the time back.
/// </summary>
public sealed class ChangeFeedCursorTests
{
    [Fact]
    public async Task The_cursor_survives_being_put_straight_into_a_query_string()
    {
        var feed = await BuildFeedAsync(DateTimeOffset.UtcNow);

        Assert.EndsWith("Z", feed.Cursor);
        Assert.DoesNotContain("+", feed.Cursor);
        Assert.Equal(feed.Cursor, Uri.EscapeDataString(feed.Cursor).Replace("%3A", ":"));
    }

    [Fact]
    public async Task The_cursor_reads_back_as_the_moment_it_was_taken()
    {
        var takenAt = DateTimeOffset.UtcNow;

        var feed = await BuildFeedAsync(takenAt);

        Assert.True(DateTimeOffset.TryParse(feed.Cursor, out var parsed), $"'{feed.Cursor}' did not parse");
        Assert.Equal(takenAt.UtcDateTime, parsed.UtcDateTime, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task A_cursor_taken_from_a_non_utc_clock_is_still_written_in_utc()
    {
        // StartCursor uses UtcNow today, but nothing about the format should depend on that.
        var localMoment = new DateTimeOffset(2026, 8, 26, 13, 20, 31, TimeSpan.FromHours(2));

        var feed = await BuildFeedAsync(localMoment);

        Assert.StartsWith("2026-08-26T11:20:31", feed.Cursor);
        Assert.EndsWith("Z", feed.Cursor);
    }

    private static Task<Contracts.Sync.ChangeFeedDto<string>> BuildFeedAsync(DateTimeOffset cursor)
        => ChangeFeed.BuildAsync<string>(
            [], cursor, Guid.NewGuid(), SyncEntityType.Note, DateTimeOffset.UnixEpoch,
            new InMemorySyncTombstoneRepository(), CancellationToken.None);
}
