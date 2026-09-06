using Orbit.Api.RateLimiting;

namespace Orbit.Api.Tests.TestDoubles;

/// <summary>
/// A shared window store that several limiters can spend from, without a database.
///
/// It counts and refuses for real rather than saying yes. A permissive double here would pass every
/// test in this file while proving nothing: the whole question is whether two instances spending from
/// one budget together stay inside it, and a store that never refuses answers it wrongly and silently.
/// </summary>
public sealed class InMemoryRateLimitWindows : IRateLimitWindows
{
    private readonly Lock _gate = new();
    private readonly Dictionary<(string Partition, DateTimeOffset WindowStart), int> _spent = [];

    /// <summary>Set to have every attempt fail the way an unreachable database does.</summary>
    public bool PretendTheDatabaseIsUnreachable { get; set; }

    public int Taken { get; private set; }

    public Task<bool> TryTakeAsync(
        string partition, DateTimeOffset windowStart, int permitLimit, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            Taken++;

            // The same answer PostgresRateLimitWindows gives when it cannot reach the count: allowed,
            // because the caller has already passed the limiter local to its own instance.
            if (PretendTheDatabaseIsUnreachable)
            {
                return Task.FromResult(true);
            }

            var key = (partition, windowStart);
            var spent = _spent.TryGetValue(key, out var already) ? already + 1 : 1;
            _spent[key] = spent;
            return Task.FromResult(spent <= permitLimit);
        }
    }

    public Task<int> DeleteWindowsClosedBeforeAsync(
        DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var closed = _spent.Keys.Where(key => key.WindowStart < cutoff).ToArray();
            foreach (var key in closed)
            {
                _spent.Remove(key);
            }

            return Task.FromResult(closed.Length);
        }
    }
}
