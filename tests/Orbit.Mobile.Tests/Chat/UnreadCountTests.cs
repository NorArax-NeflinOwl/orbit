using Orbit.Contracts.Chat;
using Xunit;

namespace Orbit.Mobile.Tests.Chat;

/// <summary>
/// How many messages are waiting from somebody, on the phone's contact list. The server has always
/// counted them (ContactDto.UnreadCount, from the read mark it keeps per conversation) and Orbit.Web has
/// always drawn the count; the phone dropped it on the way into its own store, so a list ordered by
/// recency had nothing on it saying which conversations held something new.
///
/// Kept with the row so it survives a restart and reads offline, and taken down the moment the server
/// has been told what was read - not left standing until the next refresh. How far down is
/// ConversationReadTests' question: to what has not been on screen yet.
/// </summary>
public sealed class UnreadCountTests
{
    [Fact]
    public async Task The_count_the_server_sends_is_kept_with_the_contact()
    {
        using var context = new ChatContext();

        await context.Repository.StoreContactsAsync([Contact(context, unreadCount: 3)]);

        var stored = Assert.Single(await context.Repository.GetContactsAsync());
        Assert.Equal(3, stored.UnreadCount);
        Assert.True(stored.HasSomethingWaiting);
    }

    [Fact]
    public async Task Seeing_everything_takes_the_count_to_nought()
    {
        using var context = new ChatContext();
        await context.Repository.StoreContactsAsync([Contact(context, unreadCount: 3)]);

        await context.Synchronizer.MarkConversationReadAsync(context.OtherUserId, context.Clock.GetUtcNow());

        var stored = Assert.Single(await context.Repository.GetContactsAsync());
        Assert.Equal(0, stored.UnreadCount);
        Assert.False(stored.HasSomethingWaiting);
    }

    /// <summary>
    /// Pulling the conversation is not reading it - a sync runs on a timer, in the background too - so
    /// the count stands until something has actually been on screen.
    /// </summary>
    [Fact]
    public async Task Pulling_the_conversation_leaves_the_count_alone()
    {
        using var context = new ChatContext();
        await context.Repository.StoreContactsAsync([Contact(context, unreadCount: 3)]);

        await context.Synchronizer.SynchroniseConversationAsync(context.OtherUserId);

        Assert.Equal(3, Assert.Single(await context.Repository.GetContactsAsync()).UnreadCount);
    }

    /// <summary>
    /// Read with no connection, nothing reached the server - so the count stays, as the server's own will
    /// at the next refresh. Taking it away here would say the conversation was read somewhere it was not.
    /// </summary>
    [Fact]
    public async Task A_conversation_read_offline_keeps_its_count()
    {
        using var context = new ChatContext();
        await context.Repository.StoreContactsAsync([Contact(context, unreadCount: 3)]);
        context.Server.IsUnreachable = true;

        var reached = await context.Synchronizer.MarkConversationReadAsync(context.OtherUserId, context.Clock.GetUtcNow());

        Assert.False(reached);
        Assert.Equal(3, Assert.Single(await context.Repository.GetContactsAsync()).UnreadCount);
    }

    /// <summary>The row's mark is for either thing waiting: messages to read, or a request to answer.</summary>
    [Fact]
    public async Task A_request_to_answer_marks_the_row_with_nothing_unread()
    {
        using var context = new ChatContext();

        await context.Repository.StoreContactsAsync([Contact(context, unreadCount: 0, requiresApproval: true)]);

        var stored = Assert.Single(await context.Repository.GetContactsAsync());
        Assert.Equal(0, stored.UnreadCount);
        Assert.True(stored.HasSomethingWaiting);
    }

    /// <summary>
    /// A group says exactly how many of its messages arrived since the reader last had it open - the
    /// server counts it (ChatGroupDto.UnreadCount) and the phone keeps it with the row.
    /// </summary>
    [Fact]
    public async Task A_groups_count_is_kept_with_the_group()
    {
        using var context = new ChatContext();
        WaitingIn(context, context.Server.AddGroup("Weekend trip", context.OtherUserId), unreadCount: 4);

        await context.Synchronizer.SynchroniseGroupsAsync();

        var stored = Assert.Single(await context.Repository.GetGroupsAsync());
        Assert.Equal(4, stored.UnreadCount);
        Assert.True(stored.HasSomethingWaiting);
    }

    /// <summary>
    /// The same rule the one-to-one count follows: the count goes once the server has been told the group
    /// was read, which the conversation screen says only for what has actually been on screen.
    /// </summary>
    [Fact]
    public async Task Seeing_the_groups_newest_takes_its_count_to_nought()
    {
        using var context = new ChatContext();
        var group = context.Server.AddGroup("Weekend trip", context.OtherUserId);
        WaitingIn(context, group, unreadCount: 4);
        await context.Synchronizer.SynchroniseGroupsAsync();

        await context.Synchronizer.MarkGroupConversationReadAsync(group.Id, context.Clock.GetUtcNow());

        Assert.Equal(0, Assert.Single(await context.Repository.GetGroupsAsync()).UnreadCount);
    }

    /// <summary>
    /// And pulling the group leaves it alone, for the reason the one-to-one pull does: a sync runs on a
    /// timer, in the background too, and a message pulled is not a message seen.
    /// </summary>
    [Fact]
    public async Task Pulling_a_group_conversation_leaves_its_count_alone()
    {
        using var context = new ChatContext();
        var group = context.Server.AddGroup("Weekend trip", context.OtherUserId);
        WaitingIn(context, group, unreadCount: 4);
        await context.Synchronizer.SynchroniseGroupsAsync();

        await context.Synchronizer.SynchroniseGroupConversationAsync(group.Id);

        Assert.Equal(4, Assert.Single(await context.Repository.GetGroupsAsync()).UnreadCount);
    }

    /// <summary>The server's answer about this group, as the list of groups would carry it.</summary>
    private static void WaitingIn(ChatContext context, ChatGroupDto group, int unreadCount)
    {
        var index = context.Server.Groups.FindIndex(candidate => candidate.Id == group.Id);
        context.Server.Groups[index] = group with { UnreadCount = unreadCount };
    }

    private static ContactDto Contact(ChatContext context, int unreadCount, bool requiresApproval = false)
        => new(
            context.OtherUserId, "bob", "Bob", "bob@example.com", context.OtherPublicKeyBase64,
            context.Clock.GetUtcNow(), RequiresApprovalFromCurrentUser: requiresApproval,
            IsPendingApprovalFromOtherParty: false, UnreadCount: unreadCount);
}
