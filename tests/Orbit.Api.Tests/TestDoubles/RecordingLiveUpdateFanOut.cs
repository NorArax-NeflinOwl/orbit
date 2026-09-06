using Orbit.Api.LiveUpdates;

namespace Orbit.Api.Tests.TestDoubles;

/// <summary>
/// Writes down each announcement as it was handed to the transport, instead of sending it anywhere -
/// so a test can ask what LiveUpdateAnnouncer made of a call, one layer below
/// <see cref="RecordingLiveUpdatePublisher"/>.
///
/// The message name is recorded as well as the audience, because it is the other half of the same
/// silent failure: a name the client does not listen for reaches nobody, and looks from the outside
/// exactly like the announcement that was never made.
///
/// It stands in for the local delivery, which is the narrower of the two roles - so it can also be
/// handed to PostgresLiveUpdateFanOut and PostgresLiveUpdateRelay, neither of which can be exercised
/// with a real SignalR hub behind them.
/// </summary>
public sealed class RecordingLiveUpdateFanOut : ILocalLiveUpdateFanOut
{
    public List<(string Message, IReadOnlyCollection<Guid> Audience, IReadOnlyList<object?> Arguments)>
        Announcements { get; } = [];

    public Task AnnounceAsync(
        string message,
        IReadOnlyCollection<Guid> userIds,
        IReadOnlyList<object?> arguments,
        CancellationToken cancellationToken)
    {
        Announcements.Add((message, userIds, arguments));
        return Task.CompletedTask;
    }
}
