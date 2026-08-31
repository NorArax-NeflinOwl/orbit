namespace Orbit.Contracts.Config;

/// <summary>
/// Which build of the server a client is talking to - see Orbit.Core.OrbitVersion.
///
/// Worth asking for at all because the two can differ. The pipeline deploys orbit-api and orbit-web from
/// one commit, but rolls each back on its own, so a failed API health check leaves the web client new and
/// the server old; a browser holding a cached client is the same drift by another route; and the phone is
/// released separately and updated whenever its owner chooses, which is the whole reason the version gate
/// exists. A client that shows only its own version answers "which Orbit is this" with half the truth.
/// </summary>
/// <param name="CommitHash">
/// Empty from a released server. Which commit it was cut from is detail about the inside of the
/// deployment, and a released one has no reason to hand that to whoever asks - the same rule the clients
/// apply to their own version.
/// </param>
public sealed record ServerVersionDto(string Version, string CommitHash);
