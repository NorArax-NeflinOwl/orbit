using System.Threading.RateLimiting;

namespace Orbit.Api.RateLimiting;

/// <summary>
/// A fixed window that every API instance spends from, rather than one window per process.
///
/// It is two gates, in this order, and the order is the design:
///
///  1. **This instance's own fixed window**, exactly the limiter that was here before. It is cheap, it
///     is synchronous, and it means a caller already over the budget on this replica is refused without
///     a database round trip.
///  2. **The window shared through <see cref="IRateLimitWindows"/>**, which is what makes N replicas
///     enforce the budget that was written down rather than N times it.
///
/// Because the local gate comes first and is unchanged, this can only ever refuse more than the old
/// behaviour, never less - including when the database cannot be reached at all.
///
/// A permit refused by the shared gate has already been spent on the local one. That is deliberate: a
/// fixed window does not hand permits back, and counting an attempt that was made is the conservative
/// reading of "attempts in a minute".
///
/// A <see cref="RateLimitCeiling"/> may be given as a third gate, and exists for one reason: the
/// partition of an anonymous caller is only as trustworthy as the address it was built from. If a
/// forwarded header can be forged, every request arrives in a partition of its own and the per-caller
/// gate stops meaning anything - so a bucket shared by all of them bounds what that is worth. It is set
/// far above what honest traffic looks like, because it is the thing an attacker can spend to make
/// everybody wait; the per-caller gate is what ordinary use is measured against.
/// </summary>
internal sealed class SharedFixedWindowRateLimiter : RateLimiter
{
    private static readonly RateLimitLease Refused = new Lease(acquired: false);

    private readonly FixedWindowRateLimiter _thisInstance;
    private readonly IRateLimitWindows _shared;
    private readonly string _partition;
    private readonly int _permitLimit;
    private readonly TimeSpan _window;

    private readonly RateLimitCeiling? _ceiling;

    public SharedFixedWindowRateLimiter(
        string partition, int permitLimit, TimeSpan window, IRateLimitWindows shared,
        RateLimitCeiling? ceiling = null)
    {
        _ceiling = ceiling;
        _partition = partition;
        _permitLimit = permitLimit;
        _window = window;
        _shared = shared;
        _thisInstance = new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = window,
            QueueLimit = 0
        });
    }

    public override TimeSpan? IdleDuration => _thisInstance.IdleDuration;

    public override RateLimiterStatistics? GetStatistics() => _thisInstance.GetStatistics();

    /// <summary>
    /// Always refuses, which sounds alarming and is not: the rate limiting middleware tries this first
    /// and falls back to <see cref="AcquireAsyncCore"/> when it does not succeed, so refusing here is how
    /// a limiter that needs to await something says "ask me properly". Consulting a shared window means
    /// a database round trip, and there is no honest synchronous answer to give.
    /// </summary>
    protected override RateLimitLease AttemptAcquireCore(int permitCount) => Refused;

    protected override async ValueTask<RateLimitLease> AcquireAsyncCore(
        int permitCount, CancellationToken cancellationToken)
    {
        var locally = _thisInstance.AttemptAcquire(permitCount);
        if (!locally.IsAcquired)
        {
            return locally;
        }

        var windowStart = StartOfCurrentWindow();

        var withinSharedBudget = await _shared.TryTakeAsync(
            _partition, windowStart, _permitLimit, cancellationToken);
        if (!withinSharedBudget)
        {
            return Refused;
        }

        if (_ceiling is null)
        {
            return locally;
        }

        // Only spent by attempts the per-caller gate already allowed, so a caller sitting on 429s does
        // not also drain everybody else's headroom.
        var withinCeiling = await _shared.TryTakeAsync(
            _ceiling.Partition, windowStart, _ceiling.PermitLimit, cancellationToken);

        return withinCeiling ? locally : Refused;
    }

    /// <summary>
    /// Wall-clock aligned, so every instance agrees which window an attempt falls in without any of them
    /// having to agree on when the window started. Two replicas that came up an hour apart would
    /// otherwise each be counting into a window of their own.
    /// </summary>
    private DateTimeOffset StartOfCurrentWindow()
    {
        var now = DateTimeOffset.UtcNow;
        return new DateTimeOffset(now.Ticks - (now.Ticks % _window.Ticks), TimeSpan.Zero);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _thisInstance.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override ValueTask DisposeAsyncCore() => _thisInstance.DisposeAsync();

    private sealed class Lease(bool acquired) : RateLimitLease
    {
        public override bool IsAcquired => acquired;

        public override IEnumerable<string> MetadataNames => [];

        public override bool TryGetMetadata(string metadataName, out object? metadata)
        {
            metadata = null;
            return false;
        }
    }
}
