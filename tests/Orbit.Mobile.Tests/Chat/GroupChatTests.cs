using Orbit.Mobile.Chat;
using Xunit;

namespace Orbit.Mobile.Tests.Chat;

/// <summary>
/// Group chat, and the rule the whole design is arranged around: a group message is one ciphertext per
/// current member, sealed when it goes out rather than when it is typed (info/orbit-maui-plan.md §5.5).
/// The fake server enforces what the real one does - exactly one copy per member - so a fan-out that
/// drifted from the membership fails here rather than in someone's conversation.
/// </summary>
public sealed class GroupChatTests
{
    [Fact]
    public async Task A_group_message_is_sealed_once_for_each_other_member()
    {
        using var context = new ChatContext();
        context.PublishGroupMemberKeys();
        var group = context.Server.AddGroup("Trip", context.OtherUserId, context.ThirdUserId);

        await context.Sender.SendToGroupAsync(group.Id, "we leave at six");

        // Two members other than the sender, so two copies - and each one really is for its own
        // recipient, which only their own key can show.
        var copies = context.Server.GroupMessageCopies;
        Assert.Equal(2, copies.Count);
        Assert.Equal(
            "we leave at six",
            context.OpenAsTheOtherParty(copies.Single(copy => copy.RecipientUserId == context.OtherUserId)));
        Assert.Equal(
            "we leave at six",
            context.OpenAsTheThirdParty(copies.Single(copy => copy.RecipientUserId == context.ThirdUserId)));
    }

    [Fact]
    public async Task A_message_typed_before_someone_joined_is_sent_to_the_group_as_it_is_now()
    {
        // The reason the outbox holds plaintext at all. A message encrypted when it was typed would carry
        // the membership of that moment, and the server would refuse it - correctly.
        using var context = new ChatContext();
        context.PublishGroupMemberKeys();
        var group = context.Server.AddGroup("Trip", context.OtherUserId);

        context.Server.IsUnreachable = true;
        var queued = await context.Sender.SendToGroupAsync(group.Id, "we leave at six");
        Assert.False(queued.ReachedTheServer);

        context.Server.AddMember(group.Id, context.ThirdUserId);
        context.Server.IsUnreachable = false;
        var sent = await context.Sender.FlushAsync();

        Assert.Equal(1, sent.Sent);
        Assert.Equal(2, context.Server.GroupMessageCopies.Count);
        Assert.Contains(context.Server.GroupMessageCopies, copy => copy.RecipientUserId == context.ThirdUserId);
    }

    [Fact]
    public async Task A_group_that_changes_while_a_message_is_going_out_is_tried_again_rather_than_dropped()
    {
        using var context = new ChatContext();
        context.PublishGroupMemberKeys();
        var group = context.Server.AddGroup("Trip", context.OtherUserId);

        // Someone joins after the app has read the member list and before the post lands: the fan-out is
        // one copy short, and the server refuses it.
        context.Server.WhenAGroupMessageArrives =
            () => context.Server.AddMember(group.Id, context.ThirdUserId);
        var refused = await context.Sender.SendToGroupAsync(group.Id, "we leave at six");

        Assert.Equal(0, refused.Sent);
        Assert.Equal(0, refused.GivenUp);
        // Reached the server and was refused, which is not the same as being offline.
        Assert.True(refused.ReachedTheServer);
        Assert.Empty(context.Server.GroupMessageCopies);

        context.Server.WhenAGroupMessageArrives = null;
        var sent = await context.Sender.FlushAsync();

        Assert.Equal(1, sent.Sent);
        Assert.Equal(2, context.Server.GroupMessageCopies.Count);
    }

    [Fact]
    public async Task A_member_with_no_published_key_stops_the_message_instead_of_sending_a_partial_fan_out()
    {
        using var context = new ChatContext();
        context.Users.Add(context.OtherUserId, "Bob", context.OtherPublicKeyBase64);
        context.Users.Add(context.ThirdUserId, "Carol", publicKeyBase64: null);
        var group = context.Server.AddGroup("Trip", context.OtherUserId, context.ThirdUserId);

        var result = await context.Sender.SendToGroupAsync(group.Id, "we leave at six");

        // Nothing sent at all: half a group message would quietly cut somebody out of the conversation.
        Assert.Equal(0, result.Sent);
        Assert.Equal(1, result.GivenUp);
        Assert.Empty(context.Server.GroupMessageCopies);
    }

    [Fact]
    public async Task The_sender_reads_their_own_group_message_back()
    {
        // Their copies are sealed against a recipient's key rather than their own, so this only works if
        // the reader agrees with whoever each copy was addressed to.
        using var context = new ChatContext();
        context.PublishGroupMemberKeys();
        var group = context.Server.AddGroup("Trip", context.OtherUserId, context.ThirdUserId);

        await context.Sender.SendToGroupAsync(group.Id, "we leave at six");
        await context.Synchronizer.SynchroniseGroupsAsync();
        await context.Synchronizer.SynchroniseGroupConversationAsync(group.Id);

        var conversation = await context.Reader.ReadGroupAsync(group.Id);
        var message = Assert.Single(conversation);
        Assert.True(message.IsMine);
        Assert.Equal("we leave at six", message.Text);
        Assert.Equal("You", message.SenderName);
    }

    [Fact]
    public async Task A_group_message_from_somebody_else_is_opened_and_labelled_with_who_wrote_it()
    {
        using var context = new ChatContext();
        context.PublishGroupMemberKeys();
        var group = context.Server.AddGroup("Trip", context.OtherUserId, context.ThirdUserId);

        var fromBob = context.OtherIdentity.Encrypt(context.OwnPublicKeyBase64, "running late");
        context.Server.AddIncomingGroupCopy(
            group.Id, Guid.NewGuid(), context.OtherUserId, context.OwnUserId,
            fromBob.CiphertextBase64, fromBob.NonceBase64);

        await context.Synchronizer.SynchroniseGroupsAsync();
        await context.Synchronizer.SynchroniseGroupConversationAsync(group.Id);

        var message = Assert.Single(await context.Reader.ReadGroupAsync(group.Id));
        Assert.False(message.IsMine);
        Assert.Equal("running late", message.Text);
        Assert.Equal("Bob", message.SenderName);
    }

    [Fact]
    public async Task A_synced_group_conversation_opens_with_no_connection()
    {
        // The point of caching the members' keys: opening a group message needs whichever key its copy
        // was sealed against, and that has to work without asking anybody.
        using var context = new ChatContext();
        context.PublishGroupMemberKeys();
        var group = context.Server.AddGroup("Trip", context.OtherUserId, context.ThirdUserId);

        await context.Sender.SendToGroupAsync(group.Id, "we leave at six");
        await context.Synchronizer.SynchroniseGroupsAsync();
        await context.Synchronizer.SynchroniseGroupConversationAsync(group.Id);

        context.Server.IsUnreachable = true;
        context.Users.IsUnreachable = true;

        var groups = await context.Repository.GetGroupsAsync();
        Assert.Equal("Trip", Assert.Single(groups).Name);
        Assert.Equal("we leave at six", Assert.Single(await context.Reader.ReadGroupAsync(group.Id)).Text);
    }

    [Fact]
    public async Task A_message_waiting_to_go_out_is_shown_in_the_group_it_was_typed_in()
    {
        using var context = new ChatContext();
        context.PublishGroupMemberKeys();
        var group = context.Server.AddGroup("Trip", context.OtherUserId);
        await context.Synchronizer.SynchroniseGroupsAsync();

        context.Server.IsUnreachable = true;
        await context.Sender.SendToGroupAsync(group.Id, "we leave at six");

        var message = Assert.Single(await context.Reader.ReadGroupAsync(group.Id));
        Assert.True(message.IsWaitingToSend);
        Assert.Equal("we leave at six", message.Text);
    }

    [Fact]
    public async Task Pulling_a_group_conversation_twice_reports_nothing_new_the_second_time()
    {
        // The group endpoint has no "since" and returns the whole history each time, so the sync has to
        // count what it actually stored - otherwise every poll would look like new messages arriving.
        using var context = new ChatContext();
        context.PublishGroupMemberKeys();
        var group = context.Server.AddGroup("Trip", context.OtherUserId);

        await context.Sender.SendToGroupAsync(group.Id, "we leave at six");
        await context.Synchronizer.SynchroniseGroupsAsync();

        Assert.Equal(1, (await context.Synchronizer.SynchroniseGroupConversationAsync(group.Id)).Received);
        Assert.Equal(0, (await context.Synchronizer.SynchroniseGroupConversationAsync(group.Id)).Received);
    }
}
