using Orbit.Core.Notifications;

namespace Orbit.Api.Tests.TestDoubles;

/// <summary>
/// Records what a share handler announced instead of recording a notification and dispatching a push,
/// so a test can assert that the recipient was told without standing up the notification stack.
/// </summary>
internal sealed class RecordingSharedItemNotifier : ISharedItemNotifier
{
    public List<(Guid RecipientUserId, Guid SharerUserId, SharedItemKind Kind, string? ItemTitle)> Announced { get; } = [];

    public Task NotifyAsync(
        Guid recipientUserId, Guid sharerUserId, SharedItemKind kind, string? itemTitle, CancellationToken cancellationToken)
    {
        Announced.Add((recipientUserId, sharerUserId, kind, itemTitle));
        return Task.CompletedTask;
    }
}
