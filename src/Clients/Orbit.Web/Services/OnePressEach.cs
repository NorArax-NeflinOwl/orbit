using System.Net;

namespace Orbit.Web.Services;

/// <summary>
/// One press over each of several chosen things - what the bar over a list does when somebody asks for
/// all of them to be filed or put away at once (see <see cref="PickedThings"/>).
///
/// <b>One call each rather than a bulk endpoint</b>, which is what the server already takes: filing and
/// archiving are each one request about one thing, and a handful of them is a handful of requests. That
/// was the plan recorded for this, and it is worth keeping - a bulk endpoint would need its own partial
/// failure story, which this has for free.
///
/// <b>In order rather than at once.</b> This is a browser talking to one server about a handful of
/// things; a burst of parallel writes buys nothing a reader would notice and makes the failure of any
/// one of them harder to report.
///
/// <b>A refusal stops nothing.</b> The rest are still asked: a folder one note could not be moved into
/// is no reason to leave the other four where they were. What went wrong is logged, and the caller reads
/// its page again afterwards - which is what actually shows the reader what happened.
/// </summary>
public static class OnePressEach
{
    /// <summary>
    /// Answers whether the caller should stop and go to the sign-in page: a session that has expired
    /// makes every remaining press fail the same way, so the round ends there rather than logging the
    /// same thing once per chosen thing.
    /// </summary>
    public static async Task<bool> RunAsync<T>(
        IEnumerable<T> chosen, Func<T, Task> act, ILogger logger, string what)
    {
        foreach (var thing in chosen)
        {
            try
            {
                await act(thing);
            }
            catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.Unauthorized)
            {
                return false;
            }
            catch (HttpRequestException exception)
            {
                logger.LogError(exception, "Failed to act on one of the chosen {What}", what);
            }
        }

        return true;
    }
}
