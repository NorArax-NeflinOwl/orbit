using Orbit.Localization;
using Orbit.Mobile.Chat;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Chat;

/// <summary>
/// Somebody asking to be allowed to change something of yours, from the owner's side: it arrives as an
/// ordinary encrypted message whose plaintext is structured, and the thread has to show it as the
/// request it is rather than as JSON, as nothing, or as a message that cannot be opened.
///
/// Reported on 2026-09-20 from a phone: a notification said a message had arrived and the conversation
/// held nothing. SharedItemInvitationTests covers the payload's round trip; this covers the journey.
/// </summary>
public sealed class EditAccessRequestTests
{
    [Fact]
    public async Task A_request_from_the_other_side_shows_in_the_conversation()
    {
        using var context = new ChatContext();
        var itemId = Guid.NewGuid();
        var asked = new EditAccessRequest(SharedItemKind.Note, itemId, "Shopping").ToMessage();
        var sealedUp = context.OtherIdentity.Encrypt(context.OwnPublicKeyBase64, asked);
        context.Server.AddIncoming(
            context.OtherUserId, context.OwnUserId, sealedUp.CiphertextBase64, sealedUp.NonceBase64);

        var screen = context.Conversation();
        await screen.LoadCommand.ExecuteAsync(null);

        var message = Assert.Single(screen.Messages);
        Assert.True(message.IsEditAccessRequest);
        Assert.Equal("Shopping", message.EditAccessRequestName);
        // Not the JSON, and not the placeholder for a message this device cannot open: both would be a
        // conversation that says nothing about what was asked.
        Assert.Null(message.Text);
        Assert.False(message.CannotBeOpened);
        // And it can be answered where it is read: saying yes is sharing the thing again at a level
        // that permits editing, which is the only way to widen access at all.
        Assert.True(message.CanBeAllowedToEdit);
    }

    /// <summary>
    /// Saying yes shares the thing with whoever asked, at the level they asked for and no higher -
    /// EditOnly rather than CanEdit, so they can change it without handing that on (see
    /// ShareAccessLevel).
    /// </summary>
    [Fact]
    public async Task Allowing_it_shares_the_thing_back_at_edit_level()
    {
        using var context = new ChatContext();
        context.GiveTheOtherPartyAPublishedKey();
        var itemId = Guid.NewGuid();
        var asked = new EditAccessRequest(SharedItemKind.Note, itemId, "Shopping").ToMessage();
        var sealedUp = context.OtherIdentity.Encrypt(context.OwnPublicKeyBase64, asked);
        context.Server.AddIncoming(
            context.OtherUserId, context.OwnUserId, sealedUp.CiphertextBase64, sealedUp.NonceBase64);

        var shares = new FakeShareServer();
        var screen = context.Conversation(shares);
        await screen.LoadCommand.ExecuteAsync(null);
        await screen.AllowEditingCommand.ExecuteAsync(Assert.Single(screen.Messages));

        Assert.Contains($"api/notes/{itemId}/shares", shares.Accepted);
        Assert.Equal(context.OtherUserId, shares.LastShareAsked!.RecipientUserId);
        Assert.Equal("EditOnly", shares.LastShareAsked.AccessLevel);
        Assert.Contains("Shopping", screen.Status);
    }

    /// <summary>
    /// The bubble names the kind, as Orbit.Web's own line does ("Asked to edit a note: Shopping").
    /// Until 2026-09-24 the phone said "Asked to edit" over the name and nothing else, so two requests
    /// waiting read alike and the only way to learn what either was about was to go and find it.
    /// </summary>
    [Theory]
    [InlineData(SharedItemKind.Note, "Asked to edit a note")]
    [InlineData(SharedItemKind.TaskList, "Asked to edit a task list")]
    [InlineData(SharedItemKind.CalendarEvent, "Asked to edit an event")]
    [InlineData(SharedItemKind.Inventory, "Asked to edit an inventory")]
    public void The_line_over_a_request_says_what_was_asked_about(SharedItemKind kind, string expected)
    {
        var translations = new Translations(new InMemoryLanguageStore());

        var request = new EditAccessRequest(kind, Guid.NewGuid(), "Shopping");

        // The name is not in it: the phone draws that on its own line under this one.
        Assert.Equal(expected, request.AskedToEdit(translations));
        Assert.DoesNotContain("Shopping", request.AskedToEdit(translations));
    }

    /// <summary>
    /// A place is named rather than called an inventory. Nothing asks to edit one today - a place is
    /// shared rather than worked on by somebody else - but the payload carries whatever kind it is
    /// given, and a fallback that renames the thing is worse than one that does not.
    /// </summary>
    [Fact]
    public void And_a_kind_nothing_asks_about_yet_is_still_named()
    {
        var translations = new Translations(new InMemoryLanguageStore());

        var request = new EditAccessRequest(SharedItemKind.Place, Guid.NewGuid(), "The allotment");

        Assert.Equal("Asked to edit a place", request.AskedToEdit(translations));
    }

    /// <summary>It is said in the reader's own language, both halves of it.</summary>
    [Fact]
    public void And_it_is_said_in_Polish_when_that_is_what_is_read()
    {
        var store = new InMemoryLanguageStore();
        store.Write(AppLanguage.Polish);
        var translations = new Translations(store);

        var line = new EditAccessRequest(SharedItemKind.Note, Guid.NewGuid(), "Zakupy").AskedToEdit(translations);

        Assert.Equal("Prosi o prawo edycji: notatkę", line);
    }

    /// <summary>A request of your own is something to wait on: only the owner can widen access.</summary>
    [Fact]
    public async Task Your_own_request_offers_nothing_to_press()
    {
        using var context = new ChatContext();
        context.GiveTheOtherPartyAPublishedKey();
        await context.Sender.SendAsync(
            context.OtherUserId, new EditAccessRequest(SharedItemKind.Note, Guid.NewGuid(), "Theirs").ToMessage());

        var screen = context.Conversation();
        await screen.LoadCommand.ExecuteAsync(null);

        var mine = Assert.Single(screen.Messages);
        Assert.True(mine.IsEditAccessRequest);
        Assert.False(mine.CanBeAllowedToEdit);
    }
}
