using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.PushNotifications.SubscribeToPush;
using Orbit.Core.PushNotifications.UnsubscribeFromPush;
using Xunit;

namespace Orbit.Api.Tests.PushNotifications;

public sealed class UnsubscribeFromPushCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_removes_the_subscription_and_reports_success()
    {
        var repository = new InMemoryPushSubscriptionRepository();
        var userId = Guid.NewGuid();
        await new SubscribeToPushCommandHandler(repository).HandleAsync(
            new SubscribeToPushCommand(userId, "https://push.example/a", "p256dh", "auth"), CancellationToken.None);
        var handler = new UnsubscribeFromPushCommandHandler(repository);

        var result = await handler.HandleAsync(new UnsubscribeFromPushCommand(userId, "https://push.example/a"), CancellationToken.None);

        Assert.True(result);
        Assert.Empty(await repository.GetForUserAsync(userId, CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_returns_false_when_there_was_nothing_to_remove()
    {
        var handler = new UnsubscribeFromPushCommandHandler(new InMemoryPushSubscriptionRepository());

        var result = await handler.HandleAsync(
            new UnsubscribeFromPushCommand(Guid.NewGuid(), "https://push.example/unknown"), CancellationToken.None);

        Assert.False(result);
    }
}
