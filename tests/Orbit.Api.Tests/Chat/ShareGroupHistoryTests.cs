using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Abstractions;
using Orbit.Core.Chat;
using Orbit.Core.Chat.Groups;
using Orbit.Core.Chat.Groups.GetGroupAnnouncements;
using Orbit.Core.Chat.Groups.GetGroupConversation;
using Orbit.Core.Chat.Groups.ManageChatGroupMembers;
using Orbit.Core.Chat.Groups.MarkGroupConversationAsRead;
using Orbit.Core.Chat.Groups.SendGroupMessage;
using Orbit.Core.Chat.Groups.ShareGroupHistory;
using Orbit.Core.LiveUpdates;
using Orbit.Core.Notifications;
using Xunit;

namespace Orbit.Api.Tests.Chat;

/// <summary>
/// Covers the one way a group's past can reach somebody who was not there for it: a member who can
/// already read it re-encrypting it for them. Everything here is about what the server will accept on
/// their behalf - it holds no key to any of this, so what it can check is who is asking, who they are
/// asking for, and whether they actually hold the messages they claim to be passing on.
/// </summary>
public sealed class ShareGroupHistoryTests
{
    [Fact]
    public async Task A_new_member_reads_nothing_from_before_they_joined_until_it_is_shared()
    {
        var context = new ShareGroupHistoryTestContext();
        await context.SendAsync(context.AdminId, [context.MemberId]);
        await context.AddMemberAsync(context.AdminId, context.NewcomerId);

        // No copy was ever encrypted for them, which is the whole reason this feature exists.
        Assert.Empty(await context.ReadAsync(context.NewcomerId));

        await context.ShareHistoryAsync(context.AdminId, context.NewcomerId);

        Assert.Single(await context.ReadAsync(context.NewcomerId));
    }

    [Fact]
    public async Task A_shared_message_keeps_its_original_sender_and_time()
    {
        var context = new ShareGroupHistoryTestContext();
        await context.SendAsync(context.MemberId, [context.AdminId]);
        var original = Assert.Single(await context.ReadAsync(context.AdminId));
        await context.AddMemberAsync(context.AdminId, context.NewcomerId);

        await context.ShareHistoryAsync(context.AdminId, context.NewcomerId);

        // The admin passed it on, but the member wrote it. Attribution is a fact about the message, and
        // re-sharing is not the place it gets to be restated - which is why the handler reads it off its
        // own row rather than off the request.
        var shared = Assert.Single(await context.ReadAsync(context.NewcomerId));
        Assert.Equal(context.MemberId, shared.SenderUserId);
        Assert.Equal(original.SentAtUtc, shared.SentAtUtc);
        Assert.Equal(original.GroupMessageId, shared.GroupMessageId);
        Assert.True(shared.IsSharedHistory);
    }

    [Fact]
    public async Task Only_an_admin_can_hand_over_the_history()
    {
        var context = new ShareGroupHistoryTestContext();
        await context.SendAsync(context.AdminId, [context.MemberId]);
        await context.AddMemberAsync(context.AdminId, context.NewcomerId);

        // An ordinary member can read all of this, so nothing stops them replaying it by hand. What the
        // rule buys is that the group's own history is not handed over as the group's doing by somebody
        // who was never trusted with its membership.
        var exception = await Assert.ThrowsAsync<InvalidRequestException>(
            () => context.ShareHistoryAsync(context.MemberId, context.NewcomerId));
        Assert.Contains("admin", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task History_cannot_be_pushed_at_somebody_outside_the_group()
    {
        var context = new ShareGroupHistoryTestContext();
        await context.SendAsync(context.AdminId, [context.MemberId]);

        await Assert.ThrowsAsync<InvalidRequestException>(
            () => context.ShareHistoryAsync(context.AdminId, context.OutsiderId));
    }

    [Fact]
    public async Task Somebody_outside_the_group_is_told_nothing_about_it()
    {
        var context = new ShareGroupHistoryTestContext();
        await context.SendAsync(context.AdminId, [context.MemberId]);
        await context.AddMemberAsync(context.AdminId, context.NewcomerId);

        // Not an error and not a refusal naming the group - the same silence IChatGroupRepository keeps
        // about a group you are not in.
        Assert.Equal(0, await context.ShareHistoryAsync(context.OutsiderId, context.NewcomerId));
    }

    [Fact]
    public async Task A_message_the_sharer_cannot_read_is_not_passed_on_in_their_name()
    {
        var context = new ShareGroupHistoryTestContext();
        await context.AddMemberAsync(context.AdminId, context.NewcomerId);

        // Nothing was ever posted, so this names a message the sharer holds no copy of - ciphertext they
        // would be inventing content for. Dropped rather than stored under a made-up posting.
        var written = await context.ShareHistoryAsync(context.AdminId, context.NewcomerId, [Guid.NewGuid()]);

        Assert.Equal(0, written);
        Assert.Empty(await context.ReadAsync(context.NewcomerId));
    }

    [Fact]
    public async Task Sharing_the_same_history_twice_does_not_double_it()
    {
        var context = new ShareGroupHistoryTestContext();
        await context.SendAsync(context.AdminId, [context.MemberId]);
        await context.AddMemberAsync(context.AdminId, context.NewcomerId);

        Assert.Equal(1, await context.ShareHistoryAsync(context.AdminId, context.NewcomerId));
        // A retry after a half-finished share, or a button pressed twice, must not leave the newcomer
        // reading everything in duplicate.
        Assert.Equal(0, await context.ShareHistoryAsync(context.AdminId, context.NewcomerId));

        Assert.Single(await context.ReadAsync(context.NewcomerId));
    }

    [Fact]
    public async Task A_backfilled_copy_does_not_turn_a_read_message_back_into_an_unread_one()
    {
        var context = new ShareGroupHistoryTestContext();
        await context.SendAsync(context.AdminId, [context.MemberId]);
        await context.MarkReadAsync(context.MemberId);
        await context.AddMemberAsync(context.AdminId, context.NewcomerId);

        var beforeSharing = Assert.Single(await context.ReadEntriesAsync(context.AdminId));
        Assert.True(beforeSharing.ReadByEveryone);

        await context.ShareHistoryAsync(context.AdminId, context.NewcomerId);

        // The newcomer's copy says nothing about whether the message reached the people it was posted
        // to. Counting it would have taken the sender's ticks away for a delivery that already happened.
        var afterSharing = Assert.Single(await context.ReadEntriesAsync(context.AdminId));
        Assert.True(afterSharing.ReadByEveryone);
    }

    [Fact]
    public async Task The_reader_who_already_had_a_copy_keeps_reading_the_one_they_had()
    {
        var context = new ShareGroupHistoryTestContext();
        await context.SendAsync(context.AdminId, [context.MemberId]);
        var before = Assert.Single(await context.ReadAsync(context.MemberId));
        await context.AddMemberAsync(context.AdminId, context.NewcomerId);

        await context.ShareHistoryAsync(context.AdminId, context.NewcomerId);

        // Which copy stands for a message has to stay put: the browser caches the decrypted text against
        // its id, and a choice that wandered would throw that away.
        Assert.Equal(before.Id, Assert.Single(await context.ReadAsync(context.MemberId)).Id);
    }

    [Fact]
    public async Task Joining_is_said_in_the_conversation_and_gains_the_history_half_when_it_arrives()
    {
        var context = new ShareGroupHistoryTestContext();
        await context.SendAsync(context.AdminId, [context.MemberId]);
        await context.AddMemberAsync(context.AdminId, context.NewcomerId);

        var announcement = Assert.Single(await context.AnnouncementsAsync(context.MemberId));
        Assert.Equal(context.NewcomerId, announcement.JoinedUserId);
        Assert.Equal(context.AdminId, announcement.AddedByUserId);
        // Nothing has been handed over yet, and a line promising a history that never turned up would be
        // worse than one that says only what happened.
        Assert.False(announcement.HistoryShared);

        await context.ShareHistoryAsync(context.AdminId, context.NewcomerId);

        Assert.True(Assert.Single(await context.AnnouncementsAsync(context.MemberId)).HistoryShared);
    }

    [Fact]
    public async Task A_share_that_passed_nothing_on_still_says_the_history_was_offered()
    {
        var context = new ShareGroupHistoryTestContext();
        await context.AddMemberAsync(context.AdminId, context.NewcomerId);

        // An empty group has nothing to pass on. The line still reads as it does everywhere else, rather
        // than reporting a failure at a group that simply had no past.
        Assert.Equal(0, await context.ShareHistoryAsync(context.AdminId, context.NewcomerId));

        Assert.True(Assert.Single(await context.AnnouncementsAsync(context.NewcomerId)).HistoryShared);
    }

    [Fact]
    public async Task Announcements_are_only_readable_from_inside_the_group()
    {
        var context = new ShareGroupHistoryTestContext();
        await context.AddMemberAsync(context.AdminId, context.NewcomerId);

        Assert.Empty(await context.AnnouncementsAsync(context.OutsiderId));
        Assert.NotEmpty(await context.AnnouncementsAsync(context.NewcomerId));
    }

    /// <summary>
    /// A group of two with a third person waiting outside it, which is the shape every case here needs:
    /// something said before the newcomer arrives, and somebody with no business in any of it.
    /// </summary>
    private sealed class ShareGroupHistoryTestContext
    {
        public InMemoryChatMessageRepository MessageRepository { get; } = new();
        public InMemoryChatGroupRepository GroupRepository { get; } = new();
        public InMemoryChatGroupAnnouncementRepository AnnouncementRepository { get; } = new();
        public InMemoryUserRepository UserRepository { get; } = new();
        public InMemoryContactRepository ContactRepository { get; } = new();

        public Guid AdminId { get; } = Guid.NewGuid();
        public Guid MemberId { get; } = Guid.NewGuid();
        public Guid NewcomerId { get; } = Guid.NewGuid();
        public Guid OutsiderId { get; } = Guid.NewGuid();
        public Guid GroupId { get; }

        public ShareGroupHistoryTestContext()
        {
            var group = ChatGroup.Create(AdminId, "Weekend trip");
            group.AddMember(AdminId, MemberId);
            GroupId = group.Id;
            GroupRepository.AddAsync(group, CancellationToken.None).GetAwaiter().GetResult();

            // The admin can only add people they already have a chat with - the same rule that applies at
            // creation time, and one every case here has to satisfy before it gets to the interesting part.
            ContactRepository.EnsureContactAsync(AdminId, NewcomerId, DateTimeOffset.UtcNow, CancellationToken.None)
                .GetAwaiter().GetResult();
        }

        public Task<bool> SendAsync(Guid senderId, IReadOnlyList<Guid> recipientIds)
            => new SendGroupMessageCommandHandler(GroupRepository, MessageRepository, new SilentLiveUpdatePublisher())
                .HandleAsync(
                    new SendGroupMessageCommand(
                        senderId, GroupId, recipientIds.Select(id => new GroupMessageCopy(id, $"cipher-for-{id}", "nonce")).ToList()),
                    CancellationToken.None);

        public Task<bool> AddMemberAsync(Guid actorId, Guid userId)
            => new AddChatGroupMemberCommandHandler(
                    GroupRepository, AnnouncementRepository, ContactRepository, UserRepository,
                    new NotificationRecorder(new InMemoryNotificationSettingsRepository(), new InMemoryNotificationEntryRepository(), new SilentLiveUpdatePublisher()),
                    new PushNotificationDispatcher(
                        new InMemoryPushSubscriptionRepository(), [new RecordingPushNotificationSender()],
                        NullLogger<PushNotificationDispatcher>.Instance))
                .HandleAsync(new AddChatGroupMemberCommand(actorId, GroupId, userId), CancellationToken.None);

        /// <summary>
        /// Shares everything the sharer can read unless specific postings are named - which is what the
        /// browser does, and what lets one case name a posting that does not exist.
        /// </summary>
        public async Task<int> ShareHistoryAsync(Guid actorId, Guid recipientId, IReadOnlyList<Guid>? groupMessageIds = null)
        {
            var ids = groupMessageIds ?? (await ReadAsync(actorId))
                .Where(message => message.GroupMessageId is not null)
                .Select(message => message.GroupMessageId!.Value)
                .ToList();

            return await new ShareGroupHistoryCommandHandler(GroupRepository, MessageRepository, AnnouncementRepository)
                .HandleAsync(
                    new ShareGroupHistoryCommand(
                        actorId, GroupId, recipientId,
                        ids.Select(id => new SharedHistoryCopy(id, $"re-sealed-for-{recipientId}", "nonce")).ToList()),
                    CancellationToken.None);
        }

        public async Task<IReadOnlyList<ChatMessage>> ReadAsync(Guid callerId)
            => (await ReadEntriesAsync(callerId)).Select(entry => entry.Message).ToList();

        public Task<IReadOnlyList<GroupConversationEntry>> ReadEntriesAsync(Guid callerId)
            => new GetGroupConversationQueryHandler(GroupRepository, MessageRepository)
                .HandleAsync(new GetGroupConversationQuery(callerId, GroupId), CancellationToken.None);

        public Task<IReadOnlyList<ChatGroupAnnouncement>> AnnouncementsAsync(Guid callerId)
            => new GetGroupAnnouncementsQueryHandler(GroupRepository, AnnouncementRepository)
                .HandleAsync(new GetGroupAnnouncementsQuery(callerId, GroupId), CancellationToken.None);

        public Task MarkReadAsync(Guid readerId)
            => new MarkGroupConversationAsReadCommandHandler(GroupRepository, MessageRepository, new SilentLiveUpdatePublisher())
                .HandleAsync(new MarkGroupConversationAsReadCommand(readerId, GroupId), CancellationToken.None);
    }
}
