using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.PushNotifications.SubscribeToPush;
using Xunit;

namespace Orbit.Api.Tests.PushNotifications;

public sealed class SubscribeToPushCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_stores_a_new_subscription_and_reports_success()
    {
        var repository = new InMemoryPushSubscriptionRepository();
        var handler = new SubscribeToPushCommandHandler(repository);
        var userId = Guid.NewGuid();

        var result = await handler.HandleAsync(
            new SubscribeToPushCommand(userId, "https://push.example/a", "p256dh", "auth"), CancellationToken.None);

        Assert.True(result);
        var stored = Assert.Single(await repository.GetForUserAsync(userId, CancellationToken.None));
        Assert.Equal("https://push.example/a", stored.WebPush!.Endpoint);
    }

    [Fact]
    public async Task HandleAsync_replaces_the_existing_subscription_for_the_same_endpoint()
    {
        var repository = new InMemoryPushSubscriptionRepository();
        var handler = new SubscribeToPushCommandHandler(repository);
        var userId = Guid.NewGuid();

        await handler.HandleAsync(
            new SubscribeToPushCommand(userId, "https://push.example/a", "old-p256dh", "old-auth"), CancellationToken.None);
        await handler.HandleAsync(
            new SubscribeToPushCommand(userId, "https://push.example/a", "new-p256dh", "new-auth"), CancellationToken.None);

        var stored = Assert.Single(await repository.GetForUserAsync(userId, CancellationToken.None));
        Assert.Equal("new-p256dh", stored.WebPush!.P256dhBase64);
    }
}
