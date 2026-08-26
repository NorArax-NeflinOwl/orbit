using Orbit.Mobile.Update;

namespace Orbit.Mobile.Tests.TestDoubles;

/// <summary>What Preferences holds on a device, without one.</summary>
internal sealed class InMemoryVersionVerdictCache : IVersionVerdictCache
{
    public InMemoryVersionVerdictCache(CachedVersionVerdict? remembered = null) => Remembered = remembered;

    public CachedVersionVerdict? Remembered { get; private set; }

    public Task<CachedVersionVerdict?> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(Remembered);

    public Task WriteAsync(CachedVersionVerdict verdict, CancellationToken cancellationToken)
    {
        Remembered = verdict;
        return Task.CompletedTask;
    }
}
