using Orbit.Core.LiveUpdates;

namespace Orbit.Api.Tests.TestDoubles;

/// <summary>
/// Writes down who was told what, instead of sending it anywhere - so a test can ask whether the
/// handler announced a change, and to whom.
///
/// Who matters as much as whether. Half of these announcements go to somebody other than the person
/// who caused them (a read receipt is the sender's news, not the reader's), and announcing to the
/// wrong account is invisible at runtime: the intended client hears nothing and simply falls back to
/// its slow poll, which looks exactly like the feature working slightly less well.
/// </summary>
public sealed class RecordingLiveUpdatePublisher : ILiveUpdatePublisher
{
    public List<Guid> ChatToldAbout { get; } = [];

    public List<Guid> NotificationsToldAbout { get; } = [];

    /// <summary>Each presence announcement as (whose presence changed, who was told).</summary>
    public List<(Guid Subject, IReadOnlyCollection<Guid> Audience)> PresenceAnnouncements { get; } = [];

    public Task ChatChangedAsync(Guid userId, CancellationToken cancellationToken)
    {
        ChatToldAbout.Add(userId);
        return Task.CompletedTask;
    }

    public Task ChatChangedAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken)
    {
        ChatToldAbout.AddRange(userIds);
        return Task.CompletedTask;
    }

    public Task NotificationsChangedAsync(Guid userId, CancellationToken cancellationToken)
    {
        NotificationsToldAbout.Add(userId);
        return Task.CompletedTask;
    }

    public Task PresenceChangedAsync(
        Guid userId, IReadOnlyCollection<Guid> visibleToUserIds, CancellationToken cancellationToken)
    {
        PresenceAnnouncements.Add((userId, visibleToUserIds));
        return Task.CompletedTask;
    }
}
