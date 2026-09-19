using Orbit.Contracts.Chat;
using Xunit;

namespace Orbit.Mobile.Tests.Chat;

/// <summary>
/// When a contact row says there was last anything here. The contact's own LastMessageAtUtc is bumped
/// when a message is sent and never moved back, so it goes on claiming a time after the last message is
/// deleted or the history is emptied - the conversation shows nothing and the row still says "3 days
/// ago". Asked for on 2026-09-19: the row reads the last message this phone actually holds.
///
/// Read once for the whole list, locally (ChatRepository.LastMessageTimesAsync), and used for the order
/// as well as for the words - two answers would have the list say one thing and sort by another.
/// </summary>
public sealed class LastMessageTimeTests
{
    [Fact]
    public async Task The_row_reads_the_message_rather_than_the_contact_row()
    {
        using var context = new ChatContext();
        var now = context.Clock.GetUtcNow();
        // The row claims this minute; the newest message this phone holds is a week old, which is what
        // the reader can actually see.
        await context.Repository.StoreContactsAsync([Contact(context, lastMessageAtUtc: now)]);
        await context.Repository.StoreAsync(
            context.OtherUserId, [Message(context, sentAtUtc: now.AddDays(-7))], CancellationToken.None);

        var stored = Assert.Single(await context.Repository.GetContactsAsync());

        Assert.Equal(now.AddDays(-7), stored.LastMessageShown);
    }

    /// <summary>
    /// And says nothing about a conversation this phone holds none of. It holding no messages is not the
    /// same as there being none, so the row's own answer stands rather than being replaced with silence.
    /// </summary>
    [Fact]
    public async Task A_conversation_this_phone_has_never_opened_keeps_the_rows_own_answer()
    {
        using var context = new ChatContext();
        var now = context.Clock.GetUtcNow();
        await context.Repository.StoreContactsAsync([Contact(context, lastMessageAtUtc: now.AddDays(-2))]);

        var stored = Assert.Single(await context.Repository.GetContactsAsync());

        Assert.Null(stored.LastMessageHeldAtUtc);
        Assert.Equal(now.AddDays(-2), stored.LastMessageShown);
    }

    /// <summary>
    /// A group message is sealed one copy per member, so in a two-person group a copy carries the same
    /// pair as a one-to-one message. It must not answer for the one-to-one row, or a group would make
    /// the conversation beside it look busier than it is.
    /// </summary>
    [Fact]
    public async Task A_group_message_does_not_answer_for_the_conversation_beside_it()
    {
        using var context = new ChatContext();
        var now = context.Clock.GetUtcNow();
        await context.Repository.StoreContactsAsync([Contact(context, lastMessageAtUtc: now.AddDays(-7))]);
        var groupCopy = Message(context, sentAtUtc: now) with { GroupMessageId = Guid.NewGuid() };
        await context.Repository.StoreGroupMessagesAsync(Guid.NewGuid(), [groupCopy], CancellationToken.None);

        var stored = Assert.Single(await context.Repository.GetContactsAsync());

        Assert.Null(stored.LastMessageHeldAtUtc);
    }

    private static ContactDto Contact(ChatContext context, DateTimeOffset lastMessageAtUtc)
        => new(
            context.OtherUserId, "bob", "Bob", "bob@example.com", context.OtherPublicKeyBase64,
            lastMessageAtUtc, RequiresApprovalFromCurrentUser: false, IsPendingApprovalFromOtherParty: false);

    private static ChatMessageDto Message(ChatContext context, DateTimeOffset sentAtUtc)
        => new(
            Guid.NewGuid(), context.OtherUserId, context.OwnUserId, "cipher", "nonce", sentAtUtc,
            IsEdited: false, EditedAtUtc: null);
}
