using System.Text.Json;
using Orbit.Contracts.Chat;
using Orbit.Core.Sync;
using Orbit.Mobile.Chat;
using Orbit.Localization;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Chat;

/// <summary>
/// Recognising a share offered through chat. The payloads are written by Orbit.Web with plain
/// System.Text.Json, so these serialize them exactly as it does rather than asserting against strings
/// typed out here - the risk being guarded is the two clients disagreeing about the shape, and a literal
/// would only ever agree with whoever wrote it.
/// </summary>
public sealed class ShareOfferTests
{
    [Fact]
    public void A_shared_note_is_recognised()
    {
        var shareId = Guid.NewGuid();

        var offer = ShareOffer.TryUnwrap(JsonSerializer.Serialize(new NoteShareMessagePayload(shareId, "Groceries")));

        Assert.Equal(new ShareOffer(SyncEntityType.Note, shareId, "Groceries"), offer);
    }

    [Fact]
    public void A_shared_task_list_is_recognised()
    {
        var shareId = Guid.NewGuid();

        var offer = ShareOffer.TryUnwrap(JsonSerializer.Serialize(new TaskListShareMessagePayload(shareId, "Move house")));

        Assert.Equal(new ShareOffer(SyncEntityType.TaskList, shareId, "Move house"), offer);
    }

    [Fact]
    public void A_shared_event_is_recognised()
    {
        var shareId = Guid.NewGuid();

        var offer = ShareOffer.TryUnwrap(JsonSerializer.Serialize(new EventShareMessagePayload(shareId, "Dentist")));

        Assert.Equal(new ShareOffer(SyncEntityType.CalendarEvent, shareId, "Dentist"), offer);
    }

    [Fact]
    public void A_shared_warehouse_is_recognised()
    {
        var shareId = Guid.NewGuid();

        var offer = ShareOffer.TryUnwrap(JsonSerializer.Serialize(new WarehouseShareMessagePayload(shareId, "Cellar")));

        Assert.Equal(new ShareOffer(SyncEntityType.Warehouse, shareId, "Cellar"), offer);
    }

    [Theory]
    [InlineData("Shall we meet at six?")]
    [InlineData("")]
    // Text that happens to be JSON is still text - a message reading "{}" is a message reading "{}".
    [InlineData("{}")]
    [InlineData("{\"Type\":\"orbit/something-else\",\"ShareId\":\"not even a guid\"}")]
    [InlineData("{ this never parses")]
    public void An_ordinary_message_is_not_an_offer(string plainText)
        => Assert.Null(ShareOffer.TryUnwrap(plainText));

    [Fact]
    public void A_forwarded_message_is_not_mistaken_for_a_share()
    {
        // Both travel as JSON in a message's plaintext, and both carry a Type - so the one shape that
        // could plausibly be read as the other is worth pinning down.
        var forwarded = ForwardedMessage.Wrap(
            isMine: false, Guid.NewGuid(), "Ada", "Shall we meet at six?");

        Assert.Null(ShareOffer.TryUnwrap(forwarded));
    }

    [Fact]
    public void An_offer_is_described_in_the_readers_language()
    {
        var store = new InMemoryLanguageStore();
        store.Write(AppLanguage.Polish);
        var offer = new ShareOffer(SyncEntityType.TaskList, Guid.NewGuid(), "Move house");

        Assert.Equal("Udostępnił(a) listę zadań: Move house", offer.Describe(new Translations(store)));
    }

    [Fact]
    public void A_message_carrying_an_offer_shows_the_offer_instead_of_its_text()
    {
        var message = MessageCarrying(new NoteShareMessagePayload(Guid.NewGuid(), "Groceries"));

        Assert.True(message.IsShareOffer);
        Assert.True(message.IsShareWaiting);
        Assert.False(message.ShowsItsText);
    }

    /// <summary>
    /// Forwarding one would hand somebody an offer recorded for a different recipient, and rewriting one
    /// would leave an offer pointing at nothing - so neither is offered.
    /// </summary>
    [Fact]
    public void An_offer_can_be_neither_forwarded_nor_rewritten()
    {
        var message = MessageCarrying(new NoteShareMessagePayload(Guid.NewGuid(), "Groceries")) with
        {
            IsMine = true,
            MessageId = Guid.NewGuid()
        };

        Assert.False(message.CanBeForwarded);
        Assert.False(message.CanBeChanged);
        Assert.False(message.HasActions);
    }

    [Fact]
    public void An_ordinary_message_keeps_its_text_and_its_actions()
    {
        var message = new ReadableChatMessage(
            IsMine: true, "Shall we meet at six?", DateTimeOffset.UnixEpoch, IsEdited: false,
            IsWaitingToSend: false, MessageId: Guid.NewGuid());

        Assert.False(message.IsShareOffer);
        Assert.True(message.ShowsItsText);
        Assert.True(message.CanBeForwarded);
        Assert.True(message.CanBeChanged);
    }

    private static ReadableChatMessage MessageCarrying(NoteShareMessagePayload payload)
        => new(
            IsMine: false, JsonSerializer.Serialize(payload), DateTimeOffset.UnixEpoch, IsEdited: false,
            IsWaitingToSend: false);
}
