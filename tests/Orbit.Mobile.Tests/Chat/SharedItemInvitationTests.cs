using System.Text.Json;
using Orbit.Contracts.Chat;
using Orbit.Mobile.Api;
using Orbit.Mobile.Chat;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Chat;

/// <summary>
/// Somebody offering to share something. It travels as an ordinary chat message whose plaintext is a
/// structured payload, so the server holds ciphertext and learns nothing about the offer.
///
/// The phone understood none of the four, so a note shared from the browser arrived on a phone as a blob
/// of JSON with no way to accept it - which is what these are for.
/// </summary>
public sealed class SharedItemInvitationTests
{
    [Fact]
    public void Each_kind_of_offer_is_recognised()
    {
        var shareId = Guid.NewGuid();

        Assert.Equal(
            SharedItemKind.Note,
            SharedItemInvitation.TryRead(Serialize(new NoteShareMessagePayload(shareId, "Shopping")))!.Kind);
        Assert.Equal(
            SharedItemKind.TaskList,
            SharedItemInvitation.TryRead(Serialize(new TaskListShareMessagePayload(shareId, "Trip")))!.Kind);
        Assert.Equal(
            SharedItemKind.CalendarEvent,
            SharedItemInvitation.TryRead(Serialize(new EventShareMessagePayload(shareId, "Standup")))!.Kind);
        Assert.Equal(
            SharedItemKind.Warehouse,
            SharedItemInvitation.TryRead(Serialize(new WarehouseShareMessagePayload(shareId, "Kitchen")))!.Kind);
    }

    [Fact]
    public void The_offer_carries_what_it_is_called()
    {
        var shareId = Guid.NewGuid();

        var invitation = SharedItemInvitation.TryRead(Serialize(new NoteShareMessagePayload(shareId, "Shopping")));

        Assert.Equal(shareId, invitation!.ShareId);
        Assert.Equal("Shopping", invitation.Name);
    }

    /// <summary>
    /// A message reading "{}" is a message reading "{}", not a broken payload - the same rule a forward
    /// follows, and the reason a marker is checked rather than the shape alone.
    /// </summary>
    [Theory]
    [InlineData("hello")]
    [InlineData("{}")]
    [InlineData("{ not json")]
    [InlineData("")]
    public void Ordinary_text_is_ordinary_text(string plainText)
        => Assert.Null(SharedItemInvitation.TryRead(plainText));

    /// <summary>A payload of the right shape without the marker is text somebody typed, not an offer.</summary>
    [Fact]
    public void A_payload_without_its_marker_is_not_an_offer()
        => Assert.Null(SharedItemInvitation.TryRead("""{"ShareId":"00000000-0000-0000-0000-000000000001","NoteTitle":"x"}"""));

    [Fact]
    public async Task Each_kind_is_accepted_at_its_own_endpoint()
    {
        using var server = new FakeShareServer();
        var acceptance = Build(server);
        var shareId = Guid.NewGuid();

        await acceptance.AcceptAsync(new SharedItemInvitation(SharedItemKind.Note, shareId, "Shopping"));
        await acceptance.AcceptAsync(new SharedItemInvitation(SharedItemKind.TaskList, shareId, "Trip"));
        await acceptance.AcceptAsync(new SharedItemInvitation(SharedItemKind.CalendarEvent, shareId, "Standup"));
        await acceptance.AcceptAsync(new SharedItemInvitation(SharedItemKind.Warehouse, shareId, "Kitchen"));

        Assert.Equal(
            [
                $"api/notes/shares/{shareId}/accept",
                $"api/tasks/shares/{shareId}/accept",
                $"api/calendar-events/shares/{shareId}/accept",
                $"api/warehouses/shares/{shareId}/accept"
            ],
            server.Accepted);
    }

    /// <summary>An offer already taken up, or withdrawn, is refused rather than throwing.</summary>
    [Fact]
    public async Task An_offer_that_is_gone_says_no_rather_than_failing()
    {
        using var server = new FakeShareServer { RefusesEverything = true };

        Assert.False(await Build(server).AcceptAsync(
            new SharedItemInvitation(SharedItemKind.Note, Guid.NewGuid(), "Shopping")));
    }

    private static SharedItemAcceptance Build(FakeShareServer server)
        => new(
            new NotesClient(server.ToHttpClient()), new TasksClient(server.ToHttpClient()),
            new CalendarClient(server.ToHttpClient()), new InventoryClient(server.ToHttpClient()));

    private static string Serialize<TPayload>(TPayload payload) => JsonSerializer.Serialize(payload);
}
