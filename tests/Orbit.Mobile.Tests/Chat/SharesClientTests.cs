using System.Net;
using Orbit.Core.Sync;
using Orbit.Mobile.Api;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Chat;

/// <summary>
/// Where an accept actually goes. The four kinds live under four different paths, spelled out here and
/// nowhere else on the phone, so a wrong one would fail only against a real server - and silently, since
/// the answer to a path the API does not serve is the same 404 as an offer that is no longer there.
/// </summary>
public sealed class SharesClientTests
{
    [Theory]
    [InlineData(SyncEntityType.Note, "notes")]
    [InlineData(SyncEntityType.TaskList, "tasks")]
    [InlineData(SyncEntityType.CalendarEvent, "calendar-events")]
    [InlineData(SyncEntityType.Warehouse, "warehouses")]
    public async Task Accepting_posts_to_that_kinds_shares(string entityType, string area)
    {
        var shareId = Guid.NewGuid();
        var server = StubHttpMessageHandler.RespondingWith(HttpStatusCode.NoContent);

        var accepted = await new SharesClient(server.ToHttpClient()).AcceptAsync(entityType, shareId);

        Assert.True(accepted);
        var request = Assert.Single(server.ReceivedRequests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal($"/api/{area}/shares/{shareId}/accept", request.Uri?.AbsolutePath);
    }

    [Theory]
    [InlineData(SyncEntityType.Note, "notes")]
    [InlineData(SyncEntityType.Warehouse, "warehouses")]
    public async Task Asking_whether_it_is_accepted_gets_that_kinds_status(string entityType, string area)
    {
        var shareId = Guid.NewGuid();
        var server = StubHttpMessageHandler.RespondingWith(true);

        var accepted = await new SharesClient(server.ToHttpClient()).IsAcceptedAsync(entityType, shareId);

        Assert.True(accepted);
        Assert.Equal($"/api/{area}/shares/{shareId}/status", Assert.Single(server.ReceivedRequests).Uri?.AbsolutePath);
    }

    /// <summary>
    /// An offer the server does not know - withdrawn, meant for somebody else, or about something since
    /// deleted. Unknown rather than "not accepted", so the screen can tell the two apart.
    /// </summary>
    [Fact]
    public async Task An_offer_the_server_does_not_know_has_no_status()
    {
        var client = new SharesClient(StubHttpMessageHandler.RespondingWith(HttpStatusCode.NotFound).ToHttpClient());

        Assert.Null(await client.IsAcceptedAsync(SyncEntityType.Note, Guid.NewGuid()));
    }

    [Fact]
    public async Task An_offer_the_server_will_not_have_is_not_accepted()
    {
        var client = new SharesClient(StubHttpMessageHandler.RespondingWith(HttpStatusCode.NotFound).ToHttpClient());

        Assert.False(await client.AcceptAsync(SyncEntityType.Note, Guid.NewGuid()));
    }

    /// <summary>
    /// Nothing should ever pass a kind that is not one of the four, but a share message is written by
    /// another client - so an unrecognised one is refused here rather than sent to "api//shares/...".
    /// </summary>
    [Fact]
    public async Task An_unknown_kind_is_never_sent_anywhere()
    {
        var server = StubHttpMessageHandler.RespondingWith(HttpStatusCode.NoContent);
        var client = new SharesClient(server.ToHttpClient());

        Assert.False(await client.AcceptAsync("Spaceship", Guid.NewGuid()));
        Assert.Null(await client.IsAcceptedAsync("Spaceship", Guid.NewGuid()));
        Assert.Empty(server.ReceivedRequests);
    }
}
