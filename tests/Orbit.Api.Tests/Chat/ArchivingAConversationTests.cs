using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Chat;
using Orbit.Core.Chat.Groups;
using Orbit.Core.Chat.Groups.SetGroupArchived;
using Orbit.Core.Chat.SetConversationArchived;
using Xunit;

namespace Orbit.Api.Tests.Chat;

/// <summary>
/// Putting a conversation away.
///
/// The whole point is that it is one-sided: archiving is a fact about how one person reads their own
/// screen, not about the conversation. Nothing is deleted, nobody is left, and the other party's list
/// is untouched - so the tests here are mostly about what does *not* happen.
/// </summary>
public sealed class ArchivingAConversationTests
{
    private readonly RecordingLiveUpdatePublisher _announcements = new();
    private readonly Guid _readerId = Guid.NewGuid();
    private readonly Guid _otherId = Guid.NewGuid();

    [Fact]
    public async Task Archiving_puts_it_away_on_the_readers_own_list()
    {
        var contacts = new InMemoryContactRepository();
        await contacts.EnsureContactAsync(_readerId, _otherId, DateTimeOffset.UtcNow, CancellationToken.None);

        Assert.True(await AConversationHandler(contacts).HandleAsync(
            new SetConversationArchivedCommand(_readerId, _otherId, IsArchived: true), CancellationToken.None));

        var stored = Assert.Single(await contacts.GetAllForUserAsync(_readerId, CancellationToken.None));
        Assert.True(stored.IsArchived);
    }

    /// <summary>
    /// And leaves the other person's alone. They have a row of their own, and nothing about their
    /// reading changed - so a shared "archived" flag would be one side deciding for the other.
    /// </summary>
    [Fact]
    public async Task Archiving_says_nothing_about_the_other_partys_list()
    {
        var contacts = new InMemoryContactRepository();
        await contacts.EnsureContactAsync(_readerId, _otherId, DateTimeOffset.UtcNow, CancellationToken.None);
        await contacts.EnsureContactAsync(_otherId, _readerId, DateTimeOffset.UtcNow, CancellationToken.None);

        await AConversationHandler(contacts).HandleAsync(
            new SetConversationArchivedCommand(_readerId, _otherId, IsArchived: true), CancellationToken.None);

        var theirs = Assert.Single(await contacts.GetAllForUserAsync(_otherId, CancellationToken.None));
        Assert.False(theirs.IsArchived);
        // And they are told nothing, because as far as their screen goes nothing happened.
        Assert.Equal([_readerId], _announcements.ChatToldAbout);
    }

    [Fact]
    public async Task Bringing_it_back_is_the_same_call_the_other_way()
    {
        var contacts = new InMemoryContactRepository();
        await contacts.EnsureContactAsync(_readerId, _otherId, DateTimeOffset.UtcNow, CancellationToken.None);
        await AConversationHandler(contacts).HandleAsync(
            new SetConversationArchivedCommand(_readerId, _otherId, IsArchived: true), CancellationToken.None);

        await AConversationHandler(contacts).HandleAsync(
            new SetConversationArchivedCommand(_readerId, _otherId, IsArchived: false), CancellationToken.None);

        Assert.False(Assert.Single(await contacts.GetAllForUserAsync(_readerId, CancellationToken.None)).IsArchived);
    }

    /// <summary>Somebody this reader has no row for is not a conversation they can put away.</summary>
    [Fact]
    public async Task Archiving_somebody_who_is_not_on_the_list_answers_no()
        => Assert.False(await AConversationHandler(new InMemoryContactRepository()).HandleAsync(
            new SetConversationArchivedCommand(_readerId, _otherId, IsArchived: true), CancellationToken.None));

    /// <summary>
    /// A group is archived per member. An admin putting it away must not take it off anybody else's
    /// list - which is why this needs no rank at all.
    /// </summary>
    [Fact]
    public async Task Archiving_a_group_is_one_members_view_of_it()
    {
        var adminId = Guid.NewGuid();
        var group = ChatGroup.Create(adminId, "Weekend trip");
        group.AddMember(adminId, _readerId);
        var groups = new InMemoryChatGroupRepository();
        await groups.AddAsync(group, CancellationToken.None);

        Assert.True(await AGroupHandler(groups).HandleAsync(
            new SetGroupArchivedCommand(_readerId, group.Id, IsArchived: true), CancellationToken.None));

        var stored = await groups.GetByIdAsync(group.Id, CancellationToken.None);
        Assert.True(stored!.FindMember(_readerId)!.IsArchived);
        Assert.False(stored.FindMember(adminId)!.IsArchived);
        // Still a member: archiving is not leaving.
        Assert.Equal(2, stored.Members.Count);
    }

    [Fact]
    public async Task Archiving_a_group_somebody_is_not_in_answers_no()
    {
        var group = ChatGroup.Create(Guid.NewGuid(), "Weekend trip");
        var groups = new InMemoryChatGroupRepository();
        await groups.AddAsync(group, CancellationToken.None);

        Assert.False(await AGroupHandler(groups).HandleAsync(
            new SetGroupArchivedCommand(_readerId, group.Id, IsArchived: true), CancellationToken.None));
    }

    private SetConversationArchivedCommandHandler AConversationHandler(InMemoryContactRepository contacts)
        => new(contacts, _announcements);

    private SetGroupArchivedCommandHandler AGroupHandler(InMemoryChatGroupRepository groups)
        => new(groups, _announcements);
}
