using Orbit.Mobile.Chat;
using Xunit;

namespace Orbit.Mobile.Tests.Chat;

/// <summary>
/// Whether the other party has seen what was sent them.
///
/// The server tracks this per conversation - one timestamp, the send-time of the newest message of
/// yours they have read - rather than per message, so everything sent at or before it counts as seen.
/// That shape is what these pin down, along with the two cases where saying nothing is the right answer:
/// somebody else's message, and a conversation this device could not ask about.
/// </summary>
public sealed class ReadReceiptTests
{
    [Fact]
    public async Task Opening_a_conversation_marks_what_the_other_party_sent_as_read()
    {
        // Reading is what having the conversation open *is* - there is nothing else for a reader to do.
        using var context = new ChatContext();
        var fromThem = context.OtherIdentity.Encrypt(context.OwnPublicKeyBase64, "did you see this");
        context.Server.AddIncoming(
            context.OtherUserId, context.OwnUserId, fromThem.CiphertextBase64, fromThem.NonceBase64);

        await context.Synchronizer.SynchroniseConversationAsync(context.OtherUserId);

        // Asked from their side: the message they sent is now marked.
        Assert.NotNull(context.Server.ReadUpToUtcForTheOtherParty(context.OwnUserId));
    }

    [Fact]
    public async Task Own_messages_are_marked_as_read_once_they_have_been_seen()
    {
        using var context = new ChatContext();
        context.GiveTheOtherPartyAPublishedKey();
        await context.Sender.SendAsync(context.OtherUserId, "see you at six");

        var beforeReading = Assert.Single(await context.ReadConversationAsync());
        Assert.False(beforeReading.IsReadByThem);

        context.Server.TheOtherPartyReadEverything(context.OtherUserId);
        var result = await context.Synchronizer.SynchroniseConversationAsync(context.OtherUserId);

        var afterReading = Assert.Single(await context.ReadConversationAsync(result.TheyReadUpToUtc));
        Assert.True(afterReading.IsReadByThem);
    }

    [Fact]
    public async Task Everything_sent_before_what_they_read_counts_as_read_too()
    {
        // One timestamp for the conversation, not a flag per message - so an older message is covered by
        // a newer one having been seen.
        using var context = new ChatContext();
        context.GiveTheOtherPartyAPublishedKey();
        await context.Sender.SendAsync(context.OtherUserId, "first");
        context.Clock.Advance(TimeSpan.FromMinutes(5));
        await context.Sender.SendAsync(context.OtherUserId, "second");

        context.Server.TheOtherPartyReadEverything(context.OtherUserId);
        var result = await context.Synchronizer.SynchroniseConversationAsync(context.OtherUserId);

        var conversation = await context.ReadConversationAsync(result.TheyReadUpToUtc);
        Assert.Equal(2, conversation.Count);
        Assert.All(conversation, message => Assert.True(message.IsReadByThem));
    }

    [Fact]
    public async Task A_message_from_the_other_side_never_claims_to_have_been_read()
    {
        // "Read" is about the reader's own messages. Marking somebody else's would be saying they had
        // read their own message, which means nothing.
        using var context = new ChatContext();
        var fromThem = context.OtherIdentity.Encrypt(context.OwnPublicKeyBase64, "their message");
        context.Server.AddIncoming(
            context.OtherUserId, context.OwnUserId, fromThem.CiphertextBase64, fromThem.NonceBase64);
        await context.Synchronizer.SynchroniseConversationAsync(context.OtherUserId);

        var received = Assert.Single(await context.ReadConversationAsync(context.Clock.GetUtcNow()));
        Assert.False(received.IsMine);
        Assert.False(received.IsReadByThem);
    }

    [Fact]
    public async Task A_conversation_read_offline_claims_nothing_either_way()
    {
        // Not being able to ask is not the same as nothing having been read, and the screen must not
        // turn one into the other.
        using var context = new ChatContext();
        context.GiveTheOtherPartyAPublishedKey();
        await context.Sender.SendAsync(context.OtherUserId, "see you at six");
        context.Server.TheOtherPartyReadEverything(context.OtherUserId);

        context.Server.IsUnreachable = true;
        var result = await context.Synchronizer.SynchroniseConversationAsync(context.OtherUserId);

        Assert.False(result.ReachedTheServer);
        Assert.Null(result.TheyReadUpToUtc);
        Assert.False(Assert.Single(await context.ReadConversationAsync(result.TheyReadUpToUtc)).IsReadByThem);
    }
}
