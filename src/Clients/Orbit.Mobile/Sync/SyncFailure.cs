using System.Net;

namespace Orbit.Mobile.Sync;

/// <summary>
/// Whether a failed request is one that trying again later could fix.
///
/// The distinction it exists to protect: <see cref="HttpRequestException"/> covers both "there is no
/// network" and "the server answered, with a status I did not want". Swallowing the second as though it
/// were the first tells a user whose session has expired that they are offline - wrong, and nothing they
/// can act on - so a 401 has to surface while a 500 does not.
/// </summary>
public static class SyncFailure
{
    public static bool IsWorthRetrying(Exception exception, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        return exception switch
        {
            // No response at all - the usual shape of being offline.
            HttpRequestException { StatusCode: null } => true,
            HttpRequestException { StatusCode: { } status } =>
                (int)status >= 500 || status is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests,
            TaskCanceledException => true,
            _ => false
        };
    }

    /// <summary>
    /// Whether a failed send is the outbox's to deal with - kept for another attempt, or counted against
    /// the give-up limit - rather than something that leaves its rules altogether.
    ///
    /// Everything worth retrying is. So is an answer the server will only repeat: a 400, 403, 404 or
    /// 409 to a <i>create</i>. The API clients turn those same answers to an update into a
    /// <see cref="Orbit.Mobile.Api.WriteOutcome"/>, but a create has an id to hand back and throws
    /// instead - and before this, that throw left the outbox's catch. The entry stayed queued with
    /// nothing counted, went to the server again on every sync, the corner said "Couldn't sync" and no
    /// more, and the pull behind it never ran, so the phone stopped receiving as well as sending. Seen
    /// on 2026-09-10 against an <c>orbit-api</c> old enough to answer 404 to <c>POST /api/folders</c>.
    ///
    /// A 401 still escapes. It is not about this change, and it is the one answer the reader can act on.
    /// </summary>
    public static bool StaysInTheOutbox(Exception exception, CancellationToken cancellationToken)
        => IsWorthRetrying(exception, cancellationToken)
           || exception is HttpRequestException { StatusCode: { } status } && status is not HttpStatusCode.Unauthorized;

    /// <summary>
    /// Whether the server actually answered, as opposed to there being nothing to answer.
    ///
    /// The difference decides whether a failed send counts against the outbox's give-up limit. A server
    /// that answers badly five times is refusing something, and dropping that change is the price of not
    /// blocking every change queued behind it. A phone with no signal has not been refused anything - it
    /// has not asked - and counting that would delete somebody's work for having been on a train five
    /// times, which is the exact opposite of what an outbox is for.
    /// </summary>
    public static bool WasAnswered(Exception exception)
        => exception is HttpRequestException { StatusCode: not null };
}
