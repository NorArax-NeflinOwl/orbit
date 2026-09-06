namespace Orbit.Api.RateLimiting;

/// <summary>
/// The count of attempts one caller has already spent in one window, shared by every API instance.
///
/// A separate thing from the limiter that consults it so that the limiter can be tested without a
/// database, and so that the one operation that has to be atomic is the only thing on this interface.
/// </summary>
public interface IRateLimitWindows
{
    /// <summary>
    /// Spends one permit and says whether it was within the budget. One statement, not a read followed
    /// by a write: two replicas asking at the same moment must not both see four spent and both write
    /// five.
    /// </summary>
    /// <returns>
    /// True when the caller is still inside <paramref name="permitLimit"/> for this window. **True also
    /// when the shared count could not be reached at all** - see PostgresRateLimitWindows for why that
    /// is the safe direction here rather than the dangerous one.
    /// </returns>
    Task<bool> TryTakeAsync(
        string partition, DateTimeOffset windowStart, int permitLimit, CancellationToken cancellationToken);

    /// <summary>
    /// Drops windows that have closed. A window nobody can still be inside says nothing about any future
    /// attempt, so without this the table would keep a row per caller per minute for the life of the
    /// installation.
    /// </summary>
    Task<int> DeleteWindowsClosedBeforeAsync(DateTimeOffset cutoff, CancellationToken cancellationToken);
}
