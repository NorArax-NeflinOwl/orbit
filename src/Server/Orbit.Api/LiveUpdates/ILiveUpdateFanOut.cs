namespace Orbit.Api.LiveUpdates;

/// <summary>
/// How far an announcement travels. One step below <see cref="LiveUpdateAnnouncer"/>, which decides
/// *what* is announced and to whom; this decides which connections that announcement can reach.
///
/// It exists because the honest answer changed when the API stopped being guaranteed to be one process.
/// <see cref="SignalRLiveUpdateFanOut"/> reaches the connections this instance is holding, which is
/// every connection there is for as long as a single replica runs.
/// <see cref="PostgresLiveUpdateFanOut"/> reaches the other replicas too.
///
/// Splitting it out rather than deciding inside the announcer keeps the mapping from "a message was
/// read" to "tell these two accounts" in exactly one place. That mapping is the part worth testing and
/// the part that is invisible when wrong (see LiveUpdateAnnouncementTests); it must not be written
/// twice, once per deployment topology.
/// </summary>
public interface ILiveUpdateFanOut
{
    /// <param name="arguments">
    /// Whatever the message carries, already in the order the client's handler takes it. Empty for the
    /// announcements that only say "something changed".
    /// </param>
    Task AnnounceAsync(
        string message,
        IReadOnlyCollection<Guid> userIds,
        IReadOnlyList<object?> arguments,
        CancellationToken cancellationToken);
}
