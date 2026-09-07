using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Chat;
using Orbit.Core.Chat.GetConversation;
using Xunit;

namespace Orbit.Api.Tests.Chat;

public sealed class GetConversationQueryHandlerTests
{
    [Fact]
    public async Task HandleAsync_returns_messages_from_both_directions_oldest_first()
    {
        var repository = new InMemoryChatMessageRepository();
        var userAId = Guid.NewGuid();
        var userBId = Guid.NewGuid();
        var first = ChatMessage.Create(userAId, userBId, "first", "nonce");
        await repository.AddAsync(first, CancellationToken.None);
        var second = ChatMessage.Create(userBId, userAId, "second", "nonce");
        await repository.AddAsync(second, CancellationToken.None);
        var handler = new GetConversationQueryHandler(repository, new InMemoryContactRepository());

        var messages = await handler.HandleAsync(new GetConversationQuery(userAId, userBId, null), CancellationToken.None);

        Assert.Equal([first.Id, second.Id], messages.Select(message => message.Id));
    }

    [Fact]
    public async Task HandleAsync_does_not_return_messages_between_other_users()
    {
        var repository = new InMemoryChatMessageRepository();
        await repository.AddAsync(ChatMessage.Create(Guid.NewGuid(), Guid.NewGuid(), "unrelated", "nonce"), CancellationToken.None);
        var handler = new GetConversationQueryHandler(repository, new InMemoryContactRepository());

        var messages = await handler.HandleAsync(
            new GetConversationQuery(Guid.NewGuid(), Guid.NewGuid(), null), CancellationToken.None);

        Assert.Empty(messages);
    }

    [Fact]
    public async Task HandleAsync_does_not_return_group_messages_between_the_same_two_people()
    {
        // A two-person group's messages are sealed pairwise, so a copy carries the same sender/recipient
        // pair as a one-to-one message between the two - the one-to-one conversation must still leave it
        // out, or the group's words would surface in the pair's private thread.
        var repository = new InMemoryChatMessageRepository();
        var userAId = Guid.NewGuid();
        var userBId = Guid.NewGuid();
        var oneToOne = ChatMessage.Create(userAId, userBId, "one-to-one", "nonce");
        await repository.AddAsync(oneToOne, CancellationToken.None);
        var groupCopy = ChatMessage.CreateForGroup(
            Guid.NewGuid(), Guid.NewGuid(), userBId, userAId, "in the group", "nonce", DateTimeOffset.UtcNow);
        await repository.AddAsync(groupCopy, CancellationToken.None);
        var handler = new GetConversationQueryHandler(repository, new InMemoryContactRepository());

        var messages = await handler.HandleAsync(new GetConversationQuery(userAId, userBId, null), CancellationToken.None);

        Assert.Equal([oneToOne.Id], messages.Select(message => message.Id));
    }

    [Fact]
    public async Task HandleAsync_only_returns_messages_after_sinceUtc()
    {
        var repository = new InMemoryChatMessageRepository();
        var userAId = Guid.NewGuid();
        var userBId = Guid.NewGuid();
        var older = ChatMessage.Create(userAId, userBId, "older", "nonce");
        await repository.AddAsync(older, CancellationToken.None);
        var handler = new GetConversationQueryHandler(repository, new InMemoryContactRepository());

        var messages = await handler.HandleAsync(
            new GetConversationQuery(userAId, userBId, older.SentAtUtc), CancellationToken.None);

        Assert.Empty(messages);
    }
}
