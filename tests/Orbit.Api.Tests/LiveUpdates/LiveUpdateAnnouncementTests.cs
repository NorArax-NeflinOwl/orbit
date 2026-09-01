using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Chat.DeleteMessage;
using Orbit.Core.Chat.EditMessage;
using Orbit.Core.Chat.Groups.CreateChatGroup;
using Orbit.Core.Chat.Groups.ManageChatGroupMembers;
using Orbit.Core.Chat;
using Orbit.Core.Chat.ApproveConversation;
using Orbit.Core.Chat.Groups;
using Orbit.Core.Chat.Groups.MarkGroupConversationAsRead;
using Orbit.Core.Chat.MarkConversationAsRead;
using Orbit.Core.Notifications.ClearNotifications;
using Orbit.Core.Users;
using Orbit.Core.Users.SetPresence;
using Xunit;

namespace Orbit.Api.Tests.LiveUpdates;

/// <summary>
/// Who gets told that something changed.
///
/// The audience is the part worth testing, and the reason is that getting it wrong is invisible. An
/// announcement sent to the wrong account raises nothing anywhere: the intended client simply hears
/// nothing and falls back to its slow poll, so the feature still "works", just at the speed it had
/// before any of this existed. Nothing in a running system distinguishes that from a quiet afternoon.
/// </summary>
public sealed class LiveUpdateAnnouncementTests
{
    /// <summary>
    /// A read receipt is the other party's news. The reader already knows they read it - the person
    /// waiting to see a tick appear next to what they sent is the one who has to be told.
    /// </summary>
    [Fact]
    public async Task Reading_a_conversation_tells_the_person_who_wrote_to_you()
    {
        var announcements = new RecordingLiveUpdatePublisher();
        var readerId = Guid.NewGuid();
        var otherPartyId = Guid.NewGuid();
        var handler = new MarkConversationAsReadCommandHandler(new InMemoryChatMessageRepository(), announcements);

        await handler.HandleAsync(new MarkConversationAsReadCommand(readerId, otherPartyId), CancellationToken.None);

        Assert.Equal([otherPartyId], announcements.ChatToldAbout);
    }

    /// <summary>
    /// Both, not just the person who was waiting: whoever approved it is looking at the same
    /// conversation on whatever device they approved from, and possibly on another one too.
    /// </summary>
    [Fact]
    public async Task Approving_a_conversation_tells_both_sides()
    {
        var announcements = new RecordingLiveUpdatePublisher();
        var approvingUserId = Guid.NewGuid();
        var otherPartyId = Guid.NewGuid();
        var accessRepository = new InMemoryChatConversationAccessRepository();
        await accessRepository.EnsureCreatedAsync(otherPartyId, approvingUserId, CancellationToken.None);
        var handler = new ApproveConversationCommandHandler(accessRepository, announcements);

        await handler.HandleAsync(new ApproveConversationCommand(approvingUserId, otherPartyId), CancellationToken.None);

        Assert.Equal([otherPartyId, approvingUserId], announcements.ChatToldAbout);
    }

    /// <summary>An approval that changed nothing is not news - there is no new state for anybody to read.</summary>
    [Fact]
    public async Task Approving_a_conversation_that_does_not_exist_tells_nobody()
    {
        var announcements = new RecordingLiveUpdatePublisher();
        var handler = new ApproveConversationCommandHandler(new InMemoryChatConversationAccessRepository(), announcements);

        await handler.HandleAsync(
            new ApproveConversationCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.Empty(announcements.ChatToldAbout);
    }

    /// <summary>The other members, not the reader - same reasoning as the one-to-one receipt above.</summary>
    [Fact]
    public async Task Reading_a_group_tells_the_other_members_and_not_the_reader()
    {
        var announcements = new RecordingLiveUpdatePublisher();
        var readerId = Guid.NewGuid();
        var secondMemberId = Guid.NewGuid();
        var group = ChatGroup.Create(readerId, "Weekend trip");
        group.AddMember(readerId, secondMemberId);
        var groupRepository = new InMemoryChatGroupRepository();
        await groupRepository.AddAsync(group, CancellationToken.None);
        var handler = new MarkGroupConversationAsReadCommandHandler(
            groupRepository, new InMemoryChatMessageRepository(), announcements);

        await handler.HandleAsync(
            new MarkGroupConversationAsReadCommand(readerId, group.Id), CancellationToken.None);

        Assert.Equal([secondMemberId], announcements.ChatToldAbout);
    }

    /// <summary>
    /// A heartbeat that changes nothing announces nothing. One arrives every twenty seconds per open
    /// tab and almost all of them say what everybody already believes; announcing those would be a
    /// broadcast carrying no information, sent to every contact of every signed-in account.
    /// </summary>
    [Fact]
    public async Task A_heartbeat_from_somebody_already_available_tells_nobody()
    {
        var announcements = new RecordingLiveUpdatePublisher();
        var user = AUser();
        user.RecordSeen(DateTimeOffset.UtcNow);
        var context = await AContactOfAsync(user);
        var handler = new PresenceHeartbeatCommandHandler(context.Users, context.Contacts, announcements);

        await handler.HandleAsync(new PresenceHeartbeatCommand(user.Id), CancellationToken.None);

        Assert.Empty(announcements.PresenceAnnouncements);
    }

    /// <summary>Coming back after a silence is news, and it goes to the people who can see it.</summary>
    [Fact]
    public async Task A_heartbeat_from_somebody_who_had_gone_offline_tells_their_contacts()
    {
        var announcements = new RecordingLiveUpdatePublisher();
        var user = AUser();
        user.RecordSeen(DateTimeOffset.UtcNow - UserPresence.OfflineAfter - TimeSpan.FromMinutes(1));
        var context = await AContactOfAsync(user);
        var handler = new PresenceHeartbeatCommandHandler(context.Users, context.Contacts, announcements);

        await handler.HandleAsync(new PresenceHeartbeatCommand(user.Id), CancellationToken.None);

        var announcement = Assert.Single(announcements.PresenceAnnouncements);
        Assert.Equal(user.Id, announcement.Subject);
        Assert.Equal([context.ContactUserId], announcement.Audience);
    }

    /// <summary>
    /// Choosing "do not disturb" is always news, even though the heartbeat that carries the same
    /// timestamp would not be: somebody did it on purpose, and the whole point is that others see it.
    /// </summary>
    [Fact]
    public async Task Choosing_an_availability_always_tells_the_contacts()
    {
        var announcements = new RecordingLiveUpdatePublisher();
        var user = AUser();
        user.RecordSeen(DateTimeOffset.UtcNow);
        var context = await AContactOfAsync(user);
        var handler = new SetAvailabilityCommandHandler(context.Users, context.Contacts, announcements);

        await handler.HandleAsync(
            new SetAvailabilityCommand(user.Id, PresenceAvailability.DoNotDisturb), CancellationToken.None);

        var announcement = Assert.Single(announcements.PresenceAnnouncements);
        Assert.Equal([context.ContactUserId], announcement.Audience);
    }

    private static User AUser() => User.Create("someone@example.test", "someone", "Someone", "hash");

    /// <summary>Stores the user with exactly one contact, which is who a presence announcement should reach.</summary>
    private static async Task<(InMemoryUserRepository Users, InMemoryContactRepository Contacts, Guid ContactUserId)>
        AContactOfAsync(User user)
    {
        var users = new InMemoryUserRepository();
        await users.AddAsync(user, CancellationToken.None);

        var contacts = new InMemoryContactRepository();
        var contactUserId = Guid.NewGuid();
        await contacts.EnsureContactAsync(user.Id, contactUserId, DateTimeOffset.UtcNow, CancellationToken.None);

        return (users, contacts, contactUserId);
    }

    /// <summary>An edit is the recipient's news - they are reading words that just changed under them.</summary>
    [Fact]
    public async Task Editing_a_message_tells_both_sides()
    {
        var announcements = new RecordingLiveUpdatePublisher();
        var senderId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var messages = new InMemoryChatMessageRepository();
        var message = ChatMessage.Create(senderId, recipientId, "ciphertext", "nonce");
        await messages.AddAsync(message, CancellationToken.None);
        var handler = new EditMessageCommandHandler(messages, announcements);

        await handler.HandleAsync(
            new EditMessageCommand(message.Id, senderId, "reworded", "nonce"), CancellationToken.None);

        Assert.Equal([recipientId, senderId], announcements.ChatToldAbout);
    }

    /// <summary>
    /// And so is a deletion. A message that vanishes with nobody told stays on the other person's
    /// screen until their slow poll - and they will answer something that is no longer there.
    /// </summary>
    [Fact]
    public async Task Deleting_a_message_tells_both_sides()
    {
        var announcements = new RecordingLiveUpdatePublisher();
        var senderId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var messages = new InMemoryChatMessageRepository();
        var message = ChatMessage.Create(senderId, recipientId, "ciphertext", "nonce");
        await messages.AddAsync(message, CancellationToken.None);
        var handler = new DeleteChatMessageCommandHandler(
            messages, new InMemoryChatGroupRepository(), announcements);

        await handler.HandleAsync(new DeleteChatMessageCommand(senderId, message.Id), CancellationToken.None);

        Assert.Equal([recipientId, senderId], announcements.ChatToldAbout);
    }

    /// <summary>
    /// The person removed is told as well as the people left. Their conversation list still shows the
    /// group otherwise, and a group somebody has been removed from is one they will try to write to.
    /// </summary>
    [Fact]
    public async Task Removing_somebody_tells_them_as_well_as_the_group()
    {
        var announcements = new RecordingLiveUpdatePublisher();
        var adminId = Guid.NewGuid();
        var removedId = Guid.NewGuid();
        var stayingId = Guid.NewGuid();
        var group = ChatGroup.Create(adminId, "Weekend trip");
        group.AddMember(adminId, removedId);
        group.AddMember(adminId, stayingId);
        var groups = new InMemoryChatGroupRepository();
        await groups.AddAsync(group, CancellationToken.None);
        var handler = new RemoveChatGroupMemberCommandHandler(groups, announcements);

        await handler.HandleAsync(
            new RemoveChatGroupMemberCommand(adminId, group.Id, removedId), CancellationToken.None);

        Assert.Contains(removedId, announcements.ChatToldAbout);
        Assert.Contains(stayingId, announcements.ChatToldAbout);
        Assert.Contains(adminId, announcements.ChatToldAbout);
    }

    /// <summary>
    /// A group appears in everybody's list at once and nobody but its creator asked for it, so the
    /// rest have no reason to be looking.
    /// </summary>
    [Fact]
    public async Task Making_a_group_tells_everybody_put_in_it()
    {
        var announcements = new RecordingLiveUpdatePublisher();
        var creatorId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var contacts = new InMemoryContactRepository();
        await contacts.EnsureContactAsync(creatorId, memberId, DateTimeOffset.UtcNow, CancellationToken.None);
        var handler = new CreateChatGroupCommandHandler(
            new InMemoryChatGroupRepository(), contacts, announcements);

        await handler.HandleAsync(
            new CreateChatGroupCommand(creatorId, "Weekend trip", [memberId]), CancellationToken.None);

        Assert.Contains(creatorId, announcements.ChatToldAbout);
        Assert.Contains(memberId, announcements.ChatToldAbout);
    }

    /// <summary>
    /// Clearing the panel is the reader's own doing, so the news is for their other devices - a badge
    /// cleared on a phone should not stay lit on the laptop.
    /// </summary>
    [Fact]
    public async Task Clearing_the_panel_tells_this_accounts_other_devices()
    {
        var announcements = new RecordingLiveUpdatePublisher();
        var userId = Guid.NewGuid();
        var handler = new ClearNotificationsCommandHandler(
            new InMemoryNotificationEntryRepository(), announcements);

        await handler.HandleAsync(new ClearNotificationsCommand(userId), CancellationToken.None);

        Assert.Equal([userId], announcements.NotificationsToldAbout);
    }
}
