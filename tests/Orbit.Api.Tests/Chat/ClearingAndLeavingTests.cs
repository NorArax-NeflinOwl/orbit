using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Chat;
using Orbit.Core.Chat.ClearConversationHistory;
using Orbit.Core.Chat.GetConversation;
using Orbit.Core.Chat.Groups;
using Orbit.Core.Chat.Groups.LeaveChatGroup;
using Orbit.Core.LiveUpdates;
using Xunit;

namespace Orbit.Api.Tests.Chat;

/// <summary>
/// Emptying a conversation, and walking out of a group.
///
/// The two work differently on purpose. A one-to-one message is one row that both people read, so
/// clearing hides it from the person who cleared it and leaves the other party's conversation exactly
/// as it was - taking words out of somebody else's chat is not one party's decision. A group message is
/// encrypted separately for each member, so the copies addressed to a leaver really are theirs, and
/// they go with them.
/// </summary>
public sealed class ClearingAndLeavingTests
{
    private readonly Guid _reader = Guid.NewGuid();
    private readonly Guid _otherParty = Guid.NewGuid();

    [Fact]
    public async Task Clearing_a_conversation_empties_it_for_the_reader_who_cleared_it()
    {
        var messages = new InMemoryChatMessageRepository();
        var contacts = new InMemoryContactRepository();
        await GiveThemAConversationAsync(messages, contacts);

        Assert.NotEmpty(await ReadAsync(messages, contacts, _reader));

        await ClearFor(messages, contacts).HandleAsync(
            new ClearConversationHistoryCommand(_reader, _otherParty), CancellationToken.None);

        Assert.Empty(await ReadAsync(messages, contacts, _reader));
    }

    [Fact]
    public async Task And_leaves_the_other_party_theirs()
    {
        var messages = new InMemoryChatMessageRepository();
        var contacts = new InMemoryContactRepository();
        await GiveThemAConversationAsync(messages, contacts);

        await ClearFor(messages, contacts).HandleAsync(
            new ClearConversationHistoryCommand(_reader, _otherParty), CancellationToken.None);

        Assert.NotEmpty(await ReadAsync(messages, contacts, _otherParty));
    }

    /// <summary>
    /// Clearing is not blocking: the conversation carries on, and what is said after it shows up. That
    /// is the difference between emptying a chat and ending one.
    /// </summary>
    [Fact]
    public async Task What_is_said_afterwards_still_arrives()
    {
        var messages = new InMemoryChatMessageRepository();
        var contacts = new InMemoryContactRepository();
        await GiveThemAConversationAsync(messages, contacts);
        await ClearFor(messages, contacts).HandleAsync(
            new ClearConversationHistoryCommand(_reader, _otherParty), CancellationToken.None);

        await messages.AddAsync(ChatMessage.Create(_otherParty, _reader, "later", "nonce"), CancellationToken.None);

        Assert.Single(await ReadAsync(messages, contacts, _reader));
    }

    [Fact]
    public async Task Clearing_a_conversation_with_somebody_unknown_says_so()
    {
        var messages = new InMemoryChatMessageRepository();
        var contacts = new InMemoryContactRepository();

        var cleared = await ClearFor(messages, contacts).HandleAsync(
            new ClearConversationHistoryCommand(_reader, _otherParty), CancellationToken.None);

        Assert.False(cleared);
    }

    [Fact]
    public async Task Leaving_a_group_takes_the_leavers_copies_with_them()
    {
        var groups = new InMemoryChatGroupRepository();
        var messages = new InMemoryChatMessageRepository();
        var group = ChatGroup.Create(_otherParty, "Weekend trip");
        group.AddMember(_otherParty, _reader);
        await groups.AddAsync(group, CancellationToken.None);

        var groupMessageId = Guid.NewGuid();
        await messages.AddAsync(
            ChatMessage.CreateForGroup(
                group.Id, groupMessageId, _otherParty, _reader, "for the reader", "nonce", DateTimeOffset.UtcNow),
            CancellationToken.None);
        await messages.AddAsync(
            ChatMessage.CreateForGroup(
                group.Id, groupMessageId, _otherParty, _otherParty, "for the sender", "nonce", DateTimeOffset.UtcNow),
            CancellationToken.None);

        var left = await Leave(groups, messages).HandleAsync(
            new LeaveChatGroupCommand(_reader, group.Id), CancellationToken.None);

        Assert.True(left);
        Assert.Empty(await messages.GetGroupConversationAsync(group.Id, _reader, null, CancellationToken.None));
        // The copy addressed to somebody who stayed is theirs to read, and stays.
        Assert.Single(await messages.GetGroupConversationAsync(group.Id, _otherParty, null, CancellationToken.None));
    }

    [Fact]
    public async Task And_takes_them_out_of_the_group()
    {
        var groups = new InMemoryChatGroupRepository();
        var messages = new InMemoryChatMessageRepository();
        var group = ChatGroup.Create(_otherParty, "Weekend trip");
        group.AddMember(_otherParty, _reader);
        await groups.AddAsync(group, CancellationToken.None);

        await Leave(groups, messages).HandleAsync(new LeaveChatGroupCommand(_reader, group.Id), CancellationToken.None);

        var stored = await groups.GetByIdAsync(group.Id, CancellationToken.None);
        Assert.False(stored!.IsMember(_reader));
    }

    [Fact]
    public async Task Leaving_a_group_somebody_is_not_in_says_so()
    {
        var groups = new InMemoryChatGroupRepository();
        var group = ChatGroup.Create(_otherParty, "Weekend trip");
        await groups.AddAsync(group, CancellationToken.None);

        var left = await Leave(groups, new InMemoryChatMessageRepository()).HandleAsync(
            new LeaveChatGroupCommand(_reader, group.Id), CancellationToken.None);

        Assert.False(left);
    }

    private ClearConversationHistoryCommandHandler ClearFor(
        InMemoryChatMessageRepository messages, InMemoryContactRepository contacts)
        => new(contacts, messages, new SilentLiveUpdatePublisher());

    private static LeaveChatGroupCommandHandler Leave(
        InMemoryChatGroupRepository groups, InMemoryChatMessageRepository messages)
        => new(groups, messages, new SilentLiveUpdatePublisher());

    private async Task GiveThemAConversationAsync(
        InMemoryChatMessageRepository messages, InMemoryContactRepository contacts)
    {
        var sentAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        await contacts.EnsureContactAsync(_reader, _otherParty, sentAt, CancellationToken.None);
        await contacts.EnsureContactAsync(_otherParty, _reader, sentAt, CancellationToken.None);
        await messages.AddAsync(ChatMessage.Create(_otherParty, _reader, "hello", "nonce"), CancellationToken.None);
    }

    private Task<IReadOnlyList<ChatMessage>> ReadAsync(
        InMemoryChatMessageRepository messages, InMemoryContactRepository contacts, Guid readerUserId)
        => new GetConversationQueryHandler(messages, contacts).HandleAsync(
            new GetConversationQuery(
                readerUserId,
                readerUserId == _reader ? _otherParty : _reader,
                SinceUtc: null),
            CancellationToken.None);
}
