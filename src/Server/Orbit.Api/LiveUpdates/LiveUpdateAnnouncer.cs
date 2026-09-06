using Orbit.Core.LiveUpdates;

namespace Orbit.Api.LiveUpdates;

/// <summary>
/// Turns Orbit.Core's announcements into the messages a client listens for, and hands them to whatever
/// can deliver them - see <see cref="ILiveUpdateFanOut"/>.
///
/// This is the whole of the mapping: which message name, and which accounts hear it. It is deliberately
/// the only place that decides either, because both are invisible when wrong. A message sent under a
/// name no client listens for, or to an account that was not the one waiting, raises nothing anywhere -
/// the app simply goes back to being as slow as it was before any of this existed.
/// </summary>
public sealed class LiveUpdateAnnouncer(ILiveUpdateFanOut fanOut) : ILiveUpdatePublisher
{
    private static readonly object?[] NothingToCarry = [];

    public Task ChatChangedAsync(Guid userId, CancellationToken cancellationToken)
        => fanOut.AnnounceAsync(LiveUpdateMessages.ChatChanged, [userId], NothingToCarry, cancellationToken);

    public Task ChatChangedAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken)
        => fanOut.AnnounceAsync(LiveUpdateMessages.ChatChanged, userIds, NothingToCarry, cancellationToken);

    public Task NotificationsChangedAsync(Guid userId, CancellationToken cancellationToken)
        => fanOut.AnnounceAsync(LiveUpdateMessages.NotificationsChanged, [userId], NothingToCarry, cancellationToken);

    public Task PresenceChangedAsync(
        Guid userId, IReadOnlyCollection<Guid> visibleToUserIds, CancellationToken cancellationToken)
        => fanOut.AnnounceAsync(
            LiveUpdateMessages.PresenceChanged, visibleToUserIds, [userId], cancellationToken);
}
