using Orbit.Api.Tests.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Core.Users;
using Orbit.Core.Notifications;
using Orbit.Core.Abstractions;
using Orbit.Core.Chat;
using Orbit.Core.Chat.DeleteMessage;
using Orbit.Core.Chat.Groups;
using Orbit.Core.Chat.Groups.ManageChatGroupMembers;
using Orbit.Core.Chat.Groups.GetGroupConversation;
using Orbit.Core.Chat.Groups.SendGroupMessage;
using Xunit;

namespace Orbit.Api.Tests.Chat;

/// <summary>
/// Covers what happens to a group message once ChatGroup has said who is allowed to post: the fan-out
/// into one encrypted copy per member, who can read which copy, and what deleting one takes with it.
/// </summary>
public sealed class GroupMessagingTests
{
    [Fact]
    public async Task Sending_writes_one_copy_per_other_member_and_none_for_the_sender()
    {
        var context = new GroupMessagingTestContext();

        var sent = await context.SendAsync(context.AdminId, [context.MemberId, context.SecondMemberId]);

        Assert.True(sent);
        // The sender needs no copy addressed to themselves: every copy they sent is under a pairwise key
        // they hold, so they can read the conversation back from those.
        Assert.Equal(2, context.MessageRepository.All.Count);
        Assert.All(context.MessageRepository.All, message => Assert.Equal(context.AdminId, message.SenderUserId));
        Assert.Single(context.MessageRepository.All.Select(message => message.GroupMessageId).Distinct());
    }

    [Fact]
    public async Task A_message_missing_a_copy_for_someone_is_refused()
    {
        var context = new GroupMessagingTestContext();

        // Letting this through would quietly cut that person out of a conversation they are in, and they
        // would never know a message had been sent at all.
        var exception = await Assert.ThrowsAsync<InvalidRequestException>(
            () => context.SendAsync(context.AdminId, [context.MemberId]));
        Assert.Contains("one copy for each other member", exception.Message);
    }

    [Fact]
    public async Task A_message_addressed_to_someone_outside_the_group_is_refused()
    {
        var context = new GroupMessagingTestContext();

        await Assert.ThrowsAsync<InvalidRequestException>(
            () => context.SendAsync(context.AdminId, [context.MemberId, context.SecondMemberId, context.OutsiderId]));
    }

    [Fact]
    public async Task Someone_outside_the_group_cannot_post_to_it()
    {
        var context = new GroupMessagingTestContext();

        Assert.False(await context.SendAsync(context.OutsiderId, [context.AdminId, context.MemberId, context.SecondMemberId]));
        Assert.Empty(context.MessageRepository.All);
    }

    [Fact]
    public async Task Each_member_reads_one_row_per_message_rather_than_one_per_copy()
    {
        var context = new GroupMessagingTestContext();
        await context.SendAsync(context.AdminId, [context.MemberId, context.SecondMemberId]);

        var senderView = await context.ReadAsync(context.AdminId);
        var memberView = await context.ReadAsync(context.MemberId);

        // One post, one row - for the sender too. This used to assert the sender saw both of their own
        // copies, which is the same thing as the group conversation showing their message twice; it only
        // looked harmless because a group of two stores a single copy.
        Assert.Single(senderView);
        // A member still sees only the copy encrypted for them: the other is ciphertext they hold no
        // key for, and never reaches them.
        Assert.Equal(context.MemberId, Assert.Single(memberView).RecipientUserId);
    }

    [Fact]
    public async Task A_group_message_is_not_repeated_once_per_extra_member()
    {
        var context = new GroupMessagingTestContext();

        await context.SendAsync(context.AdminId, [context.MemberId, context.SecondMemberId]);
        await context.SendAsync(context.MemberId, [context.AdminId, context.SecondMemberId]);

        // Two posts in a group of three - six stored copies between them, and two messages to read.
        Assert.Equal(2, (await context.ReadAsync(context.AdminId)).Count);
        Assert.Equal(2, (await context.ReadAsync(context.MemberId)).Count);
        Assert.Equal(2, (await context.ReadAsync(context.SecondMemberId)).Count);
    }

    [Fact]
    public async Task The_same_copy_of_a_message_is_chosen_on_every_read()
    {
        var context = new GroupMessagingTestContext();
        await context.SendAsync(context.AdminId, [context.MemberId, context.SecondMemberId]);

        var first = await context.ReadAsync(context.AdminId);
        var second = await context.ReadAsync(context.AdminId);

        // The browser caches decrypted text against the copy's id, so a choice that wandered between
        // polls would throw that cache away on every tick.
        Assert.Equal(first.Select(message => message.Id), second.Select(message => message.Id));
    }

    [Fact]
    public async Task Someone_outside_the_group_reads_nothing_from_it()
    {
        var context = new GroupMessagingTestContext();
        await context.SendAsync(context.AdminId, [context.MemberId, context.SecondMemberId]);

        Assert.Empty(await context.ReadAsync(context.OutsiderId));
    }

    [Fact]
    public async Task Deleting_a_group_message_takes_every_copy_of_it()
    {
        var context = new GroupMessagingTestContext();
        await context.SendAsync(context.AdminId, [context.MemberId, context.SecondMemberId]);
        var oneCopy = context.MessageRepository.All[0];

        Assert.True(await context.DeleteAsync(context.AdminId, oneCopy.Id));

        // Deleting one person's copy would leave the message standing for everyone else - not what
        // "delete" means anywhere else in the app.
        Assert.Empty(context.MessageRepository.All);
    }

    [Fact]
    public async Task A_member_deletes_their_own_message_but_not_someone_elses()
    {
        var context = new GroupMessagingTestContext();
        await context.SendAsync(context.MemberId, [context.AdminId, context.SecondMemberId]);
        var ownMessage = context.MessageRepository.All[0];

        Assert.True(await context.DeleteAsync(context.MemberId, ownMessage.Id));

        await context.SendAsync(context.SecondMemberId, [context.AdminId, context.MemberId]);
        var someoneElses = context.MessageRepository.All[0];
        Assert.False(await context.DeleteAsync(context.MemberId, someoneElses.Id));
        Assert.NotEmpty(context.MessageRepository.All);
    }

    [Fact]
    public async Task An_admin_deletes_anyones_message()
    {
        var context = new GroupMessagingTestContext();
        await context.SendAsync(context.MemberId, [context.AdminId, context.SecondMemberId]);

        Assert.True(await context.DeleteAsync(context.AdminId, context.MessageRepository.All[0].Id));
        Assert.Empty(context.MessageRepository.All);
    }

    [Fact]
    public async Task Only_the_sender_can_delete_a_one_to_one_message()
    {
        var context = new GroupMessagingTestContext();
        var direct = ChatMessage.Create(context.AdminId, context.MemberId, "c", "n");
        await context.MessageRepository.AddAsync(direct, CancellationToken.None);

        // Being sent something doesn't give you the right to erase it from the sender's own history.
        Assert.False(await context.DeleteAsync(context.MemberId, direct.Id));
        Assert.True(await context.DeleteAsync(context.AdminId, direct.Id));
    }


    [Fact]
    public async Task Adding_someone_already_in_the_group_needs_no_chat_with_them()
    {
        var context = new GroupMessagingTestContext();

        // The adder may have no one-to-one chat with a member who joined some other way; asking for one
        // to re-add someone already present would be friction for no gain.
        var added = await context.AddMemberAsync(context.AdminId, context.SecondMemberId);

        Assert.True(added);
    }

    [Fact]
    public async Task Being_added_to_a_group_tells_the_person_it_happened_to()
    {
        var groupRepository = new InMemoryChatGroupRepository();
        var userRepository = new InMemoryUserRepository();
        var contactRepository = new InMemoryContactRepository();
        var entryRepository = new InMemoryNotificationEntryRepository();

        var admin = User.Create("admin@example.com", "admin", "Admin", "hash");
        var invitee = User.Create("invitee@example.com", "invitee", "Invitee", "hash");
        await userRepository.AddAsync(admin, CancellationToken.None);
        await userRepository.AddAsync(invitee, CancellationToken.None);
        await contactRepository.EnsureContactAsync(admin.Id, invitee.Id, DateTimeOffset.UtcNow, CancellationToken.None);

        var group = ChatGroup.Create(admin.Id, "Weekend trip");
        await groupRepository.AddAsync(group, CancellationToken.None);

        var added = await new AddChatGroupMemberCommandHandler(
                groupRepository, contactRepository, userRepository,
                new NotificationRecorder(new InMemoryNotificationSettingsRepository(), entryRepository),
                new PushNotificationDispatcher(
                    new InMemoryPushSubscriptionRepository(), new RecordingPushNotificationSender(),
                    NullLogger<PushNotificationDispatcher>.Instance))
            .HandleAsync(new AddChatGroupMemberCommand(admin.Id, group.Id, invitee.Id), CancellationToken.None);

        // Joining a group is the one thing that happens to a member without them doing anything, so
        // without an entry it happened silently - the group just turned up in the list.
        Assert.True(added);
        var entry = Assert.Single(await entryRepository.GetRecentAsync(invitee.Id, 10, CancellationToken.None));
        Assert.Contains("Weekend trip", entry.Body);
        Assert.Contains("Admin", entry.Body);
        Assert.Equal($"/chat/groups/{group.Id}", entry.Url);
    }

    /// <summary>A group of three, wired the way DI wires the real thing.</summary>
    private sealed class GroupMessagingTestContext
    {
        public InMemoryChatMessageRepository MessageRepository { get; } = new();
        public InMemoryChatGroupRepository GroupRepository { get; } = new();
        public InMemoryUserRepository UserRepository { get; } = new();
        public InMemoryContactRepository ContactRepository { get; } = new();
        public InMemoryNotificationEntryRepository NotificationEntryRepository { get; } = new();
        public RecordingPushNotificationSender PushSender { get; } = new();
        public Guid AdminId { get; } = Guid.NewGuid();
        public Guid MemberId { get; } = Guid.NewGuid();
        public Guid SecondMemberId { get; } = Guid.NewGuid();
        public Guid OutsiderId { get; } = Guid.NewGuid();
        public Guid GroupId { get; }

        public GroupMessagingTestContext()
        {
            var group = ChatGroup.Create(AdminId, "Weekend trip");
            group.AddMember(AdminId, MemberId);
            group.AddMember(AdminId, SecondMemberId);
            GroupId = group.Id;
            GroupRepository.AddAsync(group, CancellationToken.None).GetAwaiter().GetResult();
        }

        public Task<bool> SendAsync(Guid senderId, IReadOnlyList<Guid> recipientIds)
            => new SendGroupMessageCommandHandler(GroupRepository, MessageRepository)
                .HandleAsync(
                    new SendGroupMessageCommand(
                        senderId, GroupId, recipientIds.Select(id => new GroupMessageCopy(id, $"cipher-for-{id}", "nonce")).ToList()),
                    CancellationToken.None);

        public Task<IReadOnlyList<ChatMessage>> ReadAsync(Guid callerId)
            => new GetGroupConversationQueryHandler(GroupRepository, MessageRepository)
                .HandleAsync(new GetGroupConversationQuery(callerId, GroupId), CancellationToken.None);

        public Task<bool> AddMemberAsync(Guid actorId, Guid userId)
            => new AddChatGroupMemberCommandHandler(
                    GroupRepository, ContactRepository, UserRepository,
                    new NotificationRecorder(new InMemoryNotificationSettingsRepository(), NotificationEntryRepository),
                    new PushNotificationDispatcher(
                        new InMemoryPushSubscriptionRepository(), PushSender, NullLogger<PushNotificationDispatcher>.Instance))
                .HandleAsync(new AddChatGroupMemberCommand(actorId, GroupId, userId), CancellationToken.None);


        public Task<bool> DeleteAsync(Guid actorId, Guid messageId)
            => new DeleteChatMessageCommandHandler(MessageRepository, GroupRepository)
                .HandleAsync(new DeleteChatMessageCommand(actorId, messageId), CancellationToken.None);
    }
}
