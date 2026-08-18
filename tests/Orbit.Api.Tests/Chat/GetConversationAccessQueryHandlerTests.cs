using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Chat.GetConversationAccess;
using Xunit;

namespace Orbit.Api.Tests.Chat;

public sealed class GetConversationAccessQueryHandlerTests
{
    [Fact]
    public async Task HandleAsync_returns_null_when_the_two_users_have_never_exchanged_a_message()
    {
        var handler = new GetConversationAccessQueryHandler(new InMemoryChatConversationAccessRepository());

        var access = await handler.HandleAsync(new GetConversationAccessQuery(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.Null(access);
    }

    [Fact]
    public async Task HandleAsync_finds_the_conversation_regardless_of_which_user_is_passed_first()
    {
        var repository = new InMemoryChatConversationAccessRepository();
        var initiatorId = Guid.NewGuid();
        var otherPartyId = Guid.NewGuid();
        await repository.EnsureCreatedAsync(initiatorId, otherPartyId, CancellationToken.None);
        var handler = new GetConversationAccessQueryHandler(repository);

        var access = await handler.HandleAsync(new GetConversationAccessQuery(otherPartyId, initiatorId), CancellationToken.None);

        Assert.NotNull(access);
        Assert.Equal(initiatorId, access!.InitiatedByUserId);
        Assert.False(access.IsApproved);
    }
}
