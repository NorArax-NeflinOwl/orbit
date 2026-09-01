namespace Orbit.Core.LiveUpdates;

/// <summary>
/// What a host without a live connection does with an announcement: nothing at all.
///
/// Registered by default so that every caller can announce unconditionally, rather than each one
/// checking whether anybody is listening. Nothing is lost by announcing into this - see
/// <see cref="ILiveUpdatePublisher"/> on why a missed announcement costs a delay and never a message -
/// which is what makes a do-nothing implementation the honest default rather than a stub covering a gap.
/// </summary>
public sealed class SilentLiveUpdatePublisher : ILiveUpdatePublisher
{
    public Task ChatChangedAsync(Guid userId, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task ChatChangedAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task NotificationsChangedAsync(Guid userId, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task PresenceChangedAsync(Guid userId, IReadOnlyCollection<Guid> visibleToUserIds, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
