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
        var handler = new GetConversationQueryHandler(repository);

        var messages = await handler.HandleAsync(new GetConversationQuery(userAId, userBId, null), CancellationToken.None);

        Assert.Equal([first.Id, second.Id], messages.Select(message => message.Id));
    }

    [Fact]
    public async Task HandleAsync_does_not_return_messages_between_other_users()
    {
        var repository = new InMemoryChatMessageRepository();
        await repository.AddAsync(ChatMessage.Create(Guid.NewGuid(), Guid.NewGuid(), "unrelated", "nonce"), CancellationToken.None);
        var handler = new GetConversationQueryHandler(repository);

        var messages = await handler.HandleAsync(
            new GetConversationQuery(Guid.NewGuid(), Guid.NewGuid(), null), CancellationToken.None);

        Assert.Empty(messages);
    }

    [Fact]
    public async Task HandleAsync_only_returns_messages_after_sinceUtc()
    {
        var repository = new InMemoryChatMessageRepository();
        var userAId = Guid.NewGuid();
        var userBId = Guid.NewGuid();
        var older = ChatMessage.Create(userAId, userBId, "older", "nonce");
        await repository.AddAsync(older, CancellationToken.None);
        var handler = new GetConversationQueryHandler(repository);

        var messages = await handler.HandleAsync(
            new GetConversationQuery(userAId, userBId, older.SentAtUtc), CancellationToken.None);

        Assert.Empty(messages);
    }
}
