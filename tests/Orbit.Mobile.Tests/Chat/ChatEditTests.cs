using Orbit.Mobile.Chat;
using Xunit;

namespace Orbit.Mobile.Tests.Chat;

/// <summary>
/// Rewriting and removing a message that has already been sent.
///
/// The interesting half is groups: a rewrite there is the whole fan-out again, one copy per current
/// member, because leaving a copy behind would show different members different words. The fake server
/// enforces the same one-copy-per-member rule an edit is held to as a send.
/// </summary>
public sealed class ChatEditTests
{
    [Fact]
    public async Task A_rewritten_message_reaches_the_other_party_as_the_new_text()
    {
        using var context = new ChatContext();
        context.GiveTheOtherPartyAPublishedKey();
        await context.Sender.SendAsync(context.OtherUserId, "see you at six");
        await context.Synchronizer.SynchroniseConversationAsync(context.OtherUserId);

        var sent = Assert.Single(await context.ReadConversationAsync());
        var outcome = await context.Editor.EditAsync(sent.MessageId!.Value, context.OtherUserId, "seven, sorry");

        Assert.Equal(ChatEditOutcome.Done, outcome);
        // Re-sealed rather than edited in place: the server holds ciphertext it cannot read.
        Assert.Equal("seven, sorry", context.OpenAsTheOtherParty(context.Server.Messages.Single()));
    }

    [Fact]
    public async Task A_rewritten_message_shows_its_new_text_here_without_waiting_for_a_pull()
    {
        // A one-to-one pull only asks for what is newer, so an edit to something already held would
        // never come back on its own - the phone has to write it down itself.
        using var context = new ChatContext();
        context.GiveTheOtherPartyAPublishedKey();
        await context.Sender.SendAsync(context.OtherUserId, "see you at six");

        var sent = Assert.Single(await context.ReadConversationAsync());
        await context.Editor.EditAsync(sent.MessageId!.Value, context.OtherUserId, "seven, sorry");

        var stored = Assert.Single(await context.ReadConversationAsync());
        Assert.Equal("seven, sorry", stored.Text);
        Assert.True(stored.IsEdited);
    }

    [Fact]
    public async Task A_deleted_message_goes_from_the_server_and_from_this_phone()
    {
        using var context = new ChatContext();
        context.GiveTheOtherPartyAPublishedKey();
        await context.Sender.SendAsync(context.OtherUserId, "sent by mistake");

        var sent = Assert.Single(await context.ReadConversationAsync());
        Assert.Equal(ChatEditOutcome.Done, await context.Editor.DeleteAsync(sent.MessageId!.Value));

        Assert.Empty(context.Server.Messages);
        Assert.Empty(await context.ReadConversationAsync());
    }

    [Fact]
    public async Task Rewriting_a_group_message_replaces_every_members_copy()
    {
        using var context = new ChatContext();
        context.PublishGroupMemberKeys();
        var group = context.Server.AddGroup("Trip", context.OtherUserId, context.ThirdUserId);
        await context.Sender.SendToGroupAsync(group.Id, "we leave at six");
        await context.Synchronizer.SynchroniseGroupsAsync();
        await context.Synchronizer.SynchroniseGroupConversationAsync(group.Id);

        var sent = Assert.Single(await context.Reader.ReadGroupAsync(group.Id));
        var outcome = await context.Editor.EditGroupMessageAsync(group.Id, sent.GroupMessageId!.Value, "seven, sorry");

        Assert.Equal(ChatEditOutcome.Done, outcome);
        var copies = context.Server.GroupMessageCopies;
        Assert.Equal(2, copies.Count);
        Assert.Equal(
            "seven, sorry",
            context.OpenAsTheOtherParty(copies.Single(copy => copy.RecipientUserId == context.OtherUserId)));
        Assert.Equal(
            "seven, sorry",
            context.OpenAsTheThirdParty(copies.Single(copy => copy.RecipientUserId == context.ThirdUserId)));
    }

    [Fact]
    public async Task A_rewritten_group_message_leaves_no_copy_of_what_it_said_before()
    {
        // The re-sealed copies come back under new ids, so the old ones have to be dropped or the
        // conversation would show the message twice - once with each wording.
        using var context = new ChatContext();
        context.PublishGroupMemberKeys();
        var group = context.Server.AddGroup("Trip", context.OtherUserId);
        await context.Sender.SendToGroupAsync(group.Id, "we leave at six");
        await context.Synchronizer.SynchroniseGroupsAsync();
        await context.Synchronizer.SynchroniseGroupConversationAsync(group.Id);

        var sent = Assert.Single(await context.Reader.ReadGroupAsync(group.Id));
        await context.Editor.EditGroupMessageAsync(group.Id, sent.GroupMessageId!.Value, "seven, sorry");
        await context.Synchronizer.SynchroniseGroupConversationAsync(group.Id);

        var conversation = Assert.Single(await context.Reader.ReadGroupAsync(group.Id));
        Assert.Equal("seven, sorry", conversation.Text);
    }

    [Fact]
    public async Task Deleting_one_copy_of_a_group_message_takes_the_whole_posting()
    {
        using var context = new ChatContext();
        context.PublishGroupMemberKeys();
        var group = context.Server.AddGroup("Trip", context.OtherUserId, context.ThirdUserId);
        await context.Sender.SendToGroupAsync(group.Id, "sent by mistake");
        await context.Synchronizer.SynchroniseGroupsAsync();
        await context.Synchronizer.SynchroniseGroupConversationAsync(group.Id);

        var sent = Assert.Single(await context.Reader.ReadGroupAsync(group.Id));
        Assert.Equal(ChatEditOutcome.Done, await context.Editor.DeleteAsync(sent.MessageId!.Value));

        // A message leaves the group rather than one member's view of it - both copies are gone.
        Assert.Empty(context.Server.GroupMessageCopies);
        Assert.Empty(await context.Reader.ReadGroupAsync(group.Id));
    }

    [Fact]
    public async Task Changing_a_message_with_no_connection_is_refused_rather_than_queued()
    {
        // Deliberately unlike sending. An edit is an instruction about a message the server already
        // holds; keeping one here would show this reader a history nobody else has.
        using var context = new ChatContext();
        context.GiveTheOtherPartyAPublishedKey();
        await context.Sender.SendAsync(context.OtherUserId, "see you at six");
        var sent = Assert.Single(await context.ReadConversationAsync());

        context.Server.IsUnreachable = true;

        Assert.Equal(ChatEditOutcome.Offline, await context.Editor.DeleteAsync(sent.MessageId!.Value));
        Assert.Equal(
            ChatEditOutcome.Offline,
            await context.Editor.EditAsync(sent.MessageId!.Value, context.OtherUserId, "seven, sorry"));

        context.Server.IsUnreachable = false;
        Assert.Equal("see you at six", context.OpenAsTheOtherParty(context.Server.Messages.Single()));
    }

    [Fact]
    public async Task A_message_still_waiting_to_go_out_offers_nothing_to_change()
    {
        using var context = new ChatContext();
        context.GiveTheOtherPartyAPublishedKey();
        context.Server.IsUnreachable = true;
        await context.Sender.SendAsync(context.OtherUserId, "typed with no signal");

        var queued = Assert.Single(await context.ReadConversationAsync());
        Assert.True(queued.IsWaitingToSend);
        // Nothing on the server to rewrite yet, so the screen offers neither Edit nor Delete.
        Assert.False(queued.CanBeChanged);
    }

    [Fact]
    public async Task Somebody_elses_message_is_not_offered_for_changing()
    {
        using var context = new ChatContext();
        var fromThem = context.OtherIdentity.Encrypt(context.OwnPublicKeyBase64, "their message");
        context.Server.AddIncoming(
            context.OtherUserId, context.OwnUserId, fromThem.CiphertextBase64, fromThem.NonceBase64);
        await context.Synchronizer.SynchroniseConversationAsync(context.OtherUserId);

        var received = Assert.Single(await context.ReadConversationAsync());
        Assert.False(received.IsMine);
        Assert.False(received.CanBeChanged);
    }
}
