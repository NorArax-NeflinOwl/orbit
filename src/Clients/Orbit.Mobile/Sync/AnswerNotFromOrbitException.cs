using System.Net;

namespace Orbit.Mobile.Sync;

/// <summary>
/// The address answered, and it was not Orbit.
///
/// Thrown by <see cref="OrbitAnswerHandler"/> in place of the response, so that everything downstream
/// treats it as it treats no response at all: the status code is deliberately <b>not</b> carried in
/// <see cref="HttpRequestException.StatusCode"/>, because that is the property every rule reads -
/// <see cref="SyncFailure"/> keeps such a failure out of the outbox's give-up count and calls it worth
/// retrying, and <see cref="Authentication.TokenRefreshService"/> never sees a refusal to sign out over.
/// The code the platform actually sent is kept beside it for the log.
/// </summary>
public sealed class AnswerNotFromOrbitException : HttpRequestException
{
    public AnswerNotFromOrbitException(HttpStatusCode answeredWith, Uri? requestUri)
        : base($"{requestUri} was answered {(int)answeredWith} by something other than Orbit.")
        => AnsweredWith = answeredWith;

    /// <summary>What the platform said in Orbit's place - 404 from a Container Apps front door with the app stopped.</summary>
    public HttpStatusCode AnsweredWith { get; }
}
