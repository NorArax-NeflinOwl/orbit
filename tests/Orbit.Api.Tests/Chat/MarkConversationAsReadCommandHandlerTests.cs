using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Chat;
using Orbit.Core.Chat.MarkConversationAsRead;
using Xunit;

namespace Orbit.Api.Tests.Chat;

public sealed class MarkConversationAsReadCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_marks_the_other_partys_messages_as_read()
    {
        var repository = new InMemoryChatMessageRepository();
        var readerId = Guid.NewGuid();
        var otherPartyId = Guid.NewGuid();
        var message = ChatMessage.Create(otherPartyId, readerId, "ciphertext", "nonce");
        await repository.AddAsync(message, CancellationToken.None);
        var handler = new MarkConversationAsReadCommandHandler(repository);

        var result = await handler.HandleAsync(new MarkConversationAsReadCommand(readerId, otherPartyId), CancellationToken.None);

        Assert.True(result);
        var readUpToUtc = await repository.GetReadUpToUtcAsync(otherPartyId, readerId, CancellationToken.None);
        Assert.Equal(message.SentAtUtc, readUpToUtc);
    }

    [Fact]
    public async Task HandleAsync_does_not_mark_the_readers_own_messages_as_read()
    {
        var repository = new InMemoryChatMessageRepository();
        var readerId = Guid.NewGuid();
        var otherPartyId = Guid.NewGuid();
        var ownMessage = ChatMessage.Create(readerId, otherPartyId, "ciphertext", "nonce");
        await repository.AddAsync(ownMessage, CancellationToken.None);
        var handler = new MarkConversationAsReadCommandHandler(repository);

        await handler.HandleAsync(new MarkConversationAsReadCommand(readerId, otherPartyId), CancellationToken.None);

        var readUpToUtc = await repository.GetReadUpToUtcAsync(readerId, otherPartyId, CancellationToken.None);
        Assert.Null(readUpToUtc);
    }

    [Fact]
    public async Task HandleAsync_succeeds_even_when_there_is_nothing_to_mark()
    {
        var handler = new MarkConversationAsReadCommandHandler(new InMemoryChatMessageRepository());

        var result = await handler.HandleAsync(
            new MarkConversationAsReadCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.True(result);
    }
}
