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

    /// <summary>
    /// The phone offered "Accept" on a share message for ever, with no memory of having taken it up -
    /// so a conversation reopened a week later still asked, and the only way to find out was to tap and
    /// be told no. Asked of the server rather than remembered, so an offer accepted on another device
    /// counts too.
    /// </summary>
    [Fact]
    public async Task An_offer_already_taken_up_says_so_without_being_tapped()
    {
        using var server = new FakeShareServer();
        var acceptance = Build(server);
        var taken = new SharedItemInvitation(SharedItemKind.Note, Guid.NewGuid(), "Shopping");
        var open = new SharedItemInvitation(SharedItemKind.TaskList, Guid.NewGuid(), "Trip");
        server.AlreadyTakenUp.Add(taken.ShareId);

        Assert.True(await acceptance.WasAcceptedAsync(taken));
        Assert.False(await acceptance.WasAcceptedAsync(open));
    }

    /// <summary>
    /// A question that could not be asked is not evidence the offer is gone. Still open is the answer
    /// that costs a tap when wrong; still taken is the one that hides something the reader could have.
    /// </summary>
    [Fact]
    public async Task An_offer_it_could_not_ask_about_stays_on_offer()
    {
        using var unreachable = new FakeShareServer { IsUnreachable = true };
        using var refusing = new FakeShareServer { RefusesEverything = true };

        Assert.False(await Build(unreachable).WasAcceptedAsync(
            new SharedItemInvitation(SharedItemKind.Note, Guid.NewGuid(), "Shopping")));
        Assert.False(await Build(refusing).WasAcceptedAsync(
            new SharedItemInvitation(SharedItemKind.Note, Guid.NewGuid(), "Shopping")));
    }

    /// <summary>
    /// A withdrawn offer and an accepted one both stop being offers, and the phone said "already
    /// accepted" about both - which is untrue of the first, and the reader would go looking for
    /// something they do not have.
    /// </summary>
    [Fact]
    public void An_offer_withdrawn_is_not_an_offer_accepted()
    {
        var message = new ReadableChatMessage(
            IsMine: false, Text: null, DateTimeOffset.UnixEpoch, IsEdited: false, IsWaitingToSend: false,
            Invitation: new SharedItemInvitation(SharedItemKind.Note, Guid.NewGuid(), "Shopping"));

        Assert.True(message.CanBeAccepted);
        Assert.False((message with { WasAccepted = true }).CanBeAccepted);
        Assert.False((message with { IsNoLongerOnOffer = true }).CanBeAccepted);
        // Only the one that is now theirs says so.
        Assert.False((message with { IsNoLongerOnOffer = true }).WasAccepted);
    }

    /// <summary>An offer already taken up, or withdrawn, is refused rather than throwing.</summary>
    [Fact]
    public async Task An_offer_that_is_gone_says_no_rather_than_failing()
    {
        using var server = new FakeShareServer { RefusesEverything = true };

        Assert.False(await Build(server).AcceptAsync(
            new SharedItemInvitation(SharedItemKind.Note, Guid.NewGuid(), "Shopping")));
    }

    /// <summary>
    /// Asking to be allowed to change something of somebody else's. It travels the same way an offer
    /// does and for the same reason, and it reaches the server not at all: only the owner can widen
    /// access, by sharing it again.
    /// </summary>
    [Fact]
    public void An_edit_access_request_survives_the_round_trip()
    {
        var itemId = Guid.NewGuid();

        var read = EditAccessRequest.TryRead(
            new EditAccessRequest(SharedItemKind.TaskList, itemId, "Trip").ToMessage());

        Assert.Equal(SharedItemKind.TaskList, read!.Kind);
        Assert.Equal(itemId, read.ItemId);
        Assert.Equal("Trip", read.Name);
    }

    [Theory]
    [InlineData("hello")]
    [InlineData("{}")]
    [InlineData("")]
    public void Ordinary_text_is_not_a_request(string plainText)
        => Assert.Null(EditAccessRequest.TryRead(plainText));

    /// <summary>
    /// An offer and a request are different messages and must not be read as each other - both are JSON
    /// with an id and a title, and only the marker tells them apart.
    /// </summary>
    [Fact]
    public void An_offer_is_not_read_as_a_request_nor_the_other_way_round()
    {
        var offer = Serialize(new NoteShareMessagePayload(Guid.NewGuid(), "Shopping"));
        var request = new EditAccessRequest(SharedItemKind.Note, Guid.NewGuid(), "Shopping").ToMessage();

        Assert.Null(EditAccessRequest.TryRead(offer));
        Assert.Null(SharedItemInvitation.TryRead(request));
    }

    private static SharedItemAcceptance Build(FakeShareServer server)
        => new(
            new NotesClient(server.ToHttpClient()), new TasksClient(server.ToHttpClient()),
            new CalendarClient(server.ToHttpClient()), new InventoryClient(server.ToHttpClient()));

    private static string Serialize<TPayload>(TPayload payload) => JsonSerializer.Serialize(payload);
}
