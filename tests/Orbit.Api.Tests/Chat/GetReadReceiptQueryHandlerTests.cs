using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Chat;
using Orbit.Core.Chat.GetReadReceipt;
using Orbit.Core.Chat.MarkConversationAsRead;
using Orbit.Core.LiveUpdates;
using Xunit;

namespace Orbit.Api.Tests.Chat;

public sealed class GetReadReceiptQueryHandlerTests
{
    [Fact]
    public async Task HandleAsync_returns_null_when_nothing_has_been_read_yet()
    {
        var repository = new InMemoryChatMessageRepository();
        var senderId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        await repository.AddAsync(ChatMessage.Create(senderId, recipientId, "ciphertext", "nonce"), CancellationToken.None);
        var handler = new GetReadReceiptQueryHandler(repository);

        var readUpToUtc = await handler.HandleAsync(new GetReadReceiptQuery(senderId, recipientId), CancellationToken.None);

        Assert.Null(readUpToUtc);
    }

    [Fact]
    public async Task HandleAsync_returns_the_latest_read_messages_timestamp_after_the_recipient_reads()
    {
        var repository = new InMemoryChatMessageRepository();
        var senderId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var message = ChatMessage.Create(senderId, recipientId, "ciphertext", "nonce");
        await repository.AddAsync(message, CancellationToken.None);
        var markAsReadHandler = new MarkConversationAsReadCommandHandler(repository, new SilentLiveUpdatePublisher());
        await markAsReadHandler.HandleAsync(new MarkConversationAsReadCommand(recipientId, senderId), CancellationToken.None);
        var handler = new GetReadReceiptQueryHandler(repository);

        var readUpToUtc = await handler.HandleAsync(new GetReadReceiptQuery(senderId, recipientId), CancellationToken.None);

        Assert.Equal(message.SentAtUtc, readUpToUtc);
    }
}
