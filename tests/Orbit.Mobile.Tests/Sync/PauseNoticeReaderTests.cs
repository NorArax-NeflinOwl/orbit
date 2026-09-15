using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Mobile.Sync;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Sync;

/// <summary>
/// The file the stop script writes, as the phone reads it. What these protect is the direction of every
/// doubt: a notice that cannot be read, or says nothing, is no notice - the app must never claim a
/// pause on the strength of a file it could not understand.
/// </summary>
public sealed class PauseNoticeReaderTests
{
    private static readonly Uri Address = new("https://orbitdownloads.example/apps/status.json");

    [Fact]
    public async Task The_file_the_stop_script_writes_is_read_as_a_notice()
    {
        var reader = Reading("""{"paused": true, "since": "2026-09-20T17:00:00Z", "message": "Back on the 1st."}""");

        var notice = await reader.ReadAsync(CancellationToken.None);

        Assert.Equal(new PauseNotice("Back on the 1st.", DateTimeOffset.Parse("2026-09-20T17:00:00Z")), notice);
    }

    [Fact]
    public async Task A_notice_with_no_message_still_counts()
    {
        var reader = Reading("""{"paused": true}""");

        var notice = await reader.ReadAsync(CancellationToken.None);

        Assert.Equal(new PauseNotice(string.Empty, null), notice);
    }

    [Fact]
    public async Task A_file_saying_not_paused_is_no_notice()
    {
        var reader = Reading("""{"paused": false, "message": "left behind by mistake"}""");

        Assert.Null(await reader.ReadAsync(CancellationToken.None));
    }

    [Fact]
    public async Task No_file_is_no_notice()
    {
        var reader = new PauseNoticeReader(
            StubHttpMessageHandler.RespondingWith(HttpStatusCode.NotFound).ToHttpClient(), Address,
            NullLogger<PauseNoticeReader>.Instance);

        Assert.Null(await reader.ReadAsync(CancellationToken.None));
    }

    [Fact]
    public async Task A_file_that_cannot_be_read_is_no_notice()
    {
        Assert.Null(await Reading("not json at all").ReadAsync(CancellationToken.None));

        var unreachable = new PauseNoticeReader(
            StubHttpMessageHandler.Unreachable().ToHttpClient(), Address, NullLogger<PauseNoticeReader>.Instance);
        Assert.Null(await unreachable.ReadAsync(CancellationToken.None));
    }

    /// <summary>A build told nowhere to look never reports a pause, and asks nobody.</summary>
    [Fact]
    public async Task A_build_told_no_address_never_asks()
    {
        var server = StubHttpMessageHandler.RespondingWith(HttpStatusCode.OK);
        var reader = new PauseNoticeReader(server.ToHttpClient(), address: null, NullLogger<PauseNoticeReader>.Instance);

        Assert.Null(await reader.ReadAsync(CancellationToken.None));
        Assert.Empty(server.ReceivedRequests);
    }

    private static PauseNoticeReader Reading(string json)
        => new(
            StubHttpMessageHandler.Custom((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            })).ToHttpClient(),
            Address,
            NullLogger<PauseNoticeReader>.Instance);
}
