using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Chat.ApproveConversation;
using Xunit;

namespace Orbit.Api.Tests.Chat;

public sealed class ApproveConversationCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_approves_the_conversation_for_the_non_initiating_party()
    {
        var repository = new InMemoryChatConversationAccessRepository();
        var initiatorId = Guid.NewGuid();
        var otherPartyId = Guid.NewGuid();
        await repository.EnsureCreatedAsync(initiatorId, otherPartyId, CancellationToken.None);
        var handler = new ApproveConversationCommandHandler(repository);

        var approved = await handler.HandleAsync(new ApproveConversationCommand(otherPartyId, initiatorId), CancellationToken.None);

        Assert.True(approved);
        var access = await repository.GetAsync(initiatorId, otherPartyId, CancellationToken.None);
        Assert.True(access!.IsApproved);
    }

    [Fact]
    public async Task HandleAsync_returns_false_when_no_conversation_has_been_started_yet()
    {
        var handler = new ApproveConversationCommandHandler(new InMemoryChatConversationAccessRepository());

        var approved = await handler.HandleAsync(new ApproveConversationCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.False(approved);
    }

    [Fact]
    public async Task HandleAsync_returns_false_when_the_initiator_tries_to_approve_their_own_conversation()
    {
        var repository = new InMemoryChatConversationAccessRepository();
        var initiatorId = Guid.NewGuid();
        var otherPartyId = Guid.NewGuid();
        await repository.EnsureCreatedAsync(initiatorId, otherPartyId, CancellationToken.None);
        var handler = new ApproveConversationCommandHandler(repository);

        var approved = await handler.HandleAsync(new ApproveConversationCommand(initiatorId, otherPartyId), CancellationToken.None);

        Assert.False(approved);
    }
}
