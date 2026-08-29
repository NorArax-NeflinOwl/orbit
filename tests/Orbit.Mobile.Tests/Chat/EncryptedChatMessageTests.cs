using System.Net;
using Microsoft.EntityFrameworkCore;
using Orbit.Mobile.Chat;
using Xunit;

namespace Orbit.Mobile.Tests.Chat;

/// <summary>
/// One-to-one chat: queuing what the user typed, encrypting it at the moment it goes out, and reading a
/// conversation back with no connection.
///
/// The rule these mostly defend is the one from info/orbit-maui-plan.md §5.5 - <b>encrypt at send time,
/// never at compose time</b>. It makes no visible difference for one-to-one messages, which is exactly
/// why it is worth pinning now: group chat is where getting it wrong means the server correctly rejects
/// every message written before someone joined or left, and by then four screens depend on the outbox.
/// </summary>
public sealed class EncryptedChatMessageTests
{
    [Fact]
    public async Task A_message_typed_with_no_connection_is_kept_rather_than_refused()
    {
        using var context = new ChatContext();
        context.Server.IsUnreachable = true;

        var result = await context.Sender.SendAsync(context.OtherUserId, "Written on a train");

        Assert.False(result.ReachedTheServer);
        var conversation = await context.ReadConversationAsync();
        // Shown as waiting, so it does not look lost.
        Assert.True(Assert.Single(conversation).IsWaitingToSend);
    }

    [Fact]
    public async Task What_is_queued_is_the_text_itself_not_ciphertext()
    {
        using var context = new ChatContext();
        context.Server.IsUnreachable = true;

        await context.Sender.SendAsync(context.OtherUserId, "Written on a train");

        // The queue holds plaintext on purpose: encryption cannot happen until send time, because a group
        // message's recipients are only known then. OutgoingChatMessage spells out that trade-off.
        var queued = Assert.Single(await context.Repository.GetQueuedAsync());
        Assert.Equal("Written on a train", queued.Text);
    }

    [Fact]
    public async Task Nothing_is_encrypted_until_the_message_actually_goes_out()
    {
        using var context = new ChatContext();
        context.GiveTheOtherPartyAPublishedKey();
        context.Server.IsUnreachable = true;
        await context.Sender.SendAsync(context.OtherUserId, "Written on a train");
        Assert.Empty(context.Server.Messages);

        context.Server.IsUnreachable = false;
        var result = await context.Sender.FlushAsync();

        Assert.Equal(1, result.Sent);
        // And what arrived is genuinely sealed for the recipient, not merely stored.
        Assert.Equal("Written on a train", context.OpenAsTheOtherParty(Assert.Single(context.Server.Messages)));
    }

    [Fact]
    public async Task Messages_arrive_in_the_order_they_were_typed()
    {
        using var context = new ChatContext();
        context.GiveTheOtherPartyAPublishedKey();
        context.Server.IsUnreachable = true;
        await context.Sender.SendAsync(context.OtherUserId, "First");
        await context.Sender.SendAsync(context.OtherUserId, "Second");

        context.Server.IsUnreachable = false;
        await context.Sender.FlushAsync();

        Assert.Equal(
            ["First", "Second"],
            context.Server.Messages.Select(context.OpenAsTheOtherParty));
    }

    [Fact]
    public async Task A_recipient_with_no_published_key_is_dropped_rather_than_blocking_the_queue()
    {
        using var context = new ChatContext();
        // No AddContact: nothing can be encrypted for them, and waiting will never change that.
        await context.Sender.SendAsync(context.OtherUserId, "Into the void");

        var result = await context.Sender.FlushAsync();

        Assert.Equal(0, result.Sent);
        Assert.Empty(await context.Repository.GetQueuedAsync());
    }

    [Fact]
    public async Task A_conversation_the_other_party_has_not_approved_stops_being_retried()
    {
        using var context = new ChatContext();
        context.GiveTheOtherPartyAPublishedKey();
        context.Server.RefuseSendsWith = HttpStatusCode.Forbidden;

        var result = await context.Sender.SendAsync(context.OtherUserId, "Hello?");

        // A refusal the server will repeat is not worth queueing forever.
        Assert.Equal(1, result.GivenUp);
        Assert.Empty(await context.Repository.GetQueuedAsync());

        // Dropped, so the screen has to be able to say why: the text is gone from the compose box and
        // nothing else would explain where it went.
        Assert.Equal(ChatSendRefusal.WaitingToBeAccepted, result.Refusal);
    }

    [Fact]
    public async Task Accepting_a_chat_request_is_what_lets_a_reply_through()
    {
        using var context = new ChatContext();
        context.GiveTheOtherPartyAPublishedKey();
        context.Server.RefuseSendsWith = HttpStatusCode.Forbidden;
        Assert.Equal(1, (await context.Sender.SendAsync(context.OtherUserId, "Hello?")).GivenUp);

        Assert.True(await context.ChatClient.ApproveConversationAsync(context.OtherUserId));
        var result = await context.Sender.SendAsync(context.OtherUserId, "Hello again");

        Assert.Equal(1, result.Sent);
        Assert.Equal(ChatSendRefusal.None, result.Refusal);
        Assert.Equal("Hello again", context.OpenAsTheOtherParty(context.Server.Messages.Single()));
    }

    [Fact]
    public async Task Somebody_with_no_chat_key_is_reported_rather_than_logged_and_forgotten()
    {
        using var context = new ChatContext();
        // A contact who has never signed in has no published key, so there is nothing to encrypt with.
        context.Server.AddContact(context.OtherUserId, publicKeyBase64: null);

        var result = await context.Sender.SendAsync(context.OtherUserId, "Hello?");

        Assert.Equal(1, result.GivenUp);
        Assert.Equal(ChatSendRefusal.SomebodyHasNoChatKey, result.Refusal);
    }

    [Fact]
    public async Task A_first_message_to_somebody_never_spoken_to_is_encrypted_and_sent()
    {
        // The contact list holds only people the server already counts as contacts, and it counts them
        // once a message has been sent - so the very first message to somebody found by searching has
        // nobody in that list to take a key from. Looked up by id instead, exactly as a group member is.
        using var context = new ChatContext();
        context.Users.Add(context.OtherUserId, "Bob", context.OtherPublicKeyBase64);

        var result = await context.Sender.SendAsync(context.OtherUserId, "first message");

        Assert.Equal(1, result.Sent);
        Assert.Equal(ChatSendRefusal.None, result.Refusal);
        Assert.Equal("first message", context.OpenAsTheOtherParty(context.Server.Messages.Single()));
    }

    [Fact]
    public async Task A_message_from_the_other_side_is_readable_here()
    {
        using var context = new ChatContext();
        var sealedText = context.OtherIdentity.Encrypt(context.OwnPublicKeyBase64, "Reply from the other side");
        context.Server.AddIncoming(
            context.OtherUserId, context.OwnUserId, sealedText.CiphertextBase64, sealedText.NonceBase64);

        await context.Synchronizer.SynchroniseConversationAsync(context.OtherUserId);

        var message = Assert.Single(await context.ReadConversationAsync());
        Assert.Equal("Reply from the other side", message.Text);
        Assert.False(message.IsMine);
    }

    [Fact]
    public async Task A_conversation_can_be_read_again_with_no_connection()
    {
        using var context = new ChatContext();
        var sealedText = context.OtherIdentity.Encrypt(context.OwnPublicKeyBase64, "Said earlier");
        context.Server.AddIncoming(
            context.OtherUserId, context.OwnUserId, sealedText.CiphertextBase64, sealedText.NonceBase64);
        await context.Synchronizer.SynchroniseConversationAsync(context.OtherUserId);

        context.Server.IsUnreachable = true;
        var result = await context.Synchronizer.SynchroniseConversationAsync(context.OtherUserId);

        // History stays readable offline, which is what caching it locally is for.
        Assert.False(result.ReachedTheServer);
        Assert.Equal("Said earlier", Assert.Single(await context.ReadConversationAsync()).Text);
    }

    [Fact]
    public async Task A_later_pull_asks_only_for_what_arrived_since_the_last_one()
    {
        using var context = new ChatContext();
        var first = context.OtherIdentity.Encrypt(context.OwnPublicKeyBase64, "First");
        context.Server.AddIncoming(context.OtherUserId, context.OwnUserId, first.CiphertextBase64, first.NonceBase64);
        await context.Synchronizer.SynchroniseConversationAsync(context.OtherUserId);

        context.Clock.Advance(TimeSpan.FromMinutes(1));
        var second = context.OtherIdentity.Encrypt(context.OwnPublicKeyBase64, "Second");
        context.Server.AddIncoming(context.OtherUserId, context.OwnUserId, second.CiphertextBase64, second.NonceBase64);
        var result = await context.Synchronizer.SynchroniseConversationAsync(context.OtherUserId);

        // Only the new one came down the wire, but both are in the conversation.
        Assert.Equal(1, result.Received);
        Assert.Equal(["First", "Second"], (await context.ReadConversationAsync()).Select(message => message.Text));
    }

    [Fact]
    public async Task A_message_that_cannot_be_opened_shows_as_a_gap_rather_than_failing_the_conversation()
    {
        using var context = new ChatContext();
        using var stranger = Orbit.Mobile.Crypto.ChatIdentity.Create();
        var unreadable = stranger.Encrypt(stranger.PublicKeyBase64, "Sealed under a key pair since replaced");
        context.Server.AddIncoming(
            context.OtherUserId, context.OwnUserId, unreadable.CiphertextBase64, unreadable.NonceBase64);

        await context.Synchronizer.SynchroniseConversationAsync(context.OtherUserId);

        // One unreadable message must not take the whole screen down with it.
        Assert.Null(Assert.Single(await context.ReadConversationAsync()).Text);
    }

    [Fact]
    public async Task A_sent_message_stops_showing_as_waiting()
    {
        using var context = new ChatContext();
        context.GiveTheOtherPartyAPublishedKey();

        await context.Sender.SendAsync(context.OtherUserId, "Sent straight away");

        var message = Assert.Single(await context.ReadConversationAsync());
        Assert.False(message.IsWaitingToSend);
        Assert.True(message.IsMine);
        Assert.Equal("Sent straight away", message.Text);
    }

    [Fact]
    public async Task The_people_a_user_talks_to_are_readable_with_no_connection()
    {
        using var context = new ChatContext();
        context.GiveTheOtherPartyAPublishedKey();
        await context.Synchronizer.SynchroniseContactsAsync();

        context.Server.IsUnreachable = true;
        var refreshed = await context.Synchronizer.SynchroniseContactsAsync();

        // Without this a conversation whose history is cached still could not be reached, which made
        // offline chat readable in principle and not in practice.
        Assert.False(refreshed);
        Assert.Equal(context.OtherUserId, Assert.Single(await context.Repository.GetContactsAsync()).UserId);
    }

    [Fact]
    public async Task Someone_who_has_dropped_off_the_servers_list_drops_off_here_too()
    {
        using var context = new ChatContext();
        context.GiveTheOtherPartyAPublishedKey();
        await context.Synchronizer.SynchroniseContactsAsync();

        context.Server.Contacts.Clear();
        await context.Synchronizer.SynchroniseContactsAsync();

        // The server's list is the complete answer, so refreshing replaces rather than merges.
        Assert.Empty(await context.Repository.GetContactsAsync());
    }

    [Fact]
    public async Task A_cached_contact_carries_the_key_needed_to_know_they_can_be_written_to()
    {
        using var context = new ChatContext();
        context.GiveTheOtherPartyAPublishedKey();

        await context.Synchronizer.SynchroniseContactsAsync();

        // Held for display - "can this person be written to at all" - and deliberately not used to
        // encrypt, which always fetches the key fresh. See LocalContact.
        Assert.Equal(context.OtherPublicKeyBase64, Assert.Single(await context.Repository.GetContactsAsync()).PublicKeyBase64);
    }

    [Fact]
    public async Task Syncing_a_conversation_offline_reports_it_rather_than_throwing()
    {
        using var context = new ChatContext();
        context.Server.IsUnreachable = true;

        var result = await context.Synchronizer.SynchroniseConversationAsync(context.OtherUserId);

        Assert.False(result.ReachedTheServer);
    }
}
