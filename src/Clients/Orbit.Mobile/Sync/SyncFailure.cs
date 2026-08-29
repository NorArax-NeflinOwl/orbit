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
}
