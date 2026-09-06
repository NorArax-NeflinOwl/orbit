using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Orbit.Api.Instances;

namespace Orbit.Api.Telemetry;

/// <summary>
/// Remembers, per account and for a minute, whether somebody asked to be left out of the trace this
/// deployment keeps - see <see cref="TraceOptOut"/>, which reads it on every authenticated request.
///
/// The cache exists because the alternative is a database read on every request in Orbit, which is a
/// high price for a flag almost nobody sets. What makes that acceptable is
/// <see cref="ForgetEverywhereAsync"/>: the endpoint that changes the choice clears the entry, so
/// neither direction waits the minute out.
///
/// **Everywhere is the operative word, and it is why this is a service rather than two static methods.**
/// The entry lives in one process's memory, so on a second replica clearing it locally leaves every
/// other instance tracing an account that has just asked not to be. That is not a stale read to be
/// traded away - it is the privacy guarantee itself, so the change is announced to the other instances
/// over the notice bus.
/// </summary>
public sealed class PrivacyChoiceCache(
    IMemoryCache cache,
    PostgresInstanceNoticeSender notices)
{
    public const string Channel = "orbit_privacy_choice_changed";

    private static readonly TimeSpan RememberedFor = TimeSpan.FromMinutes(1);

    public bool TryRecall(Guid userId, out bool keepsThirdPartiesOut)
        => cache.TryGetValue(CacheKey(userId), out keepsThirdPartiesOut);

    public void Remember(Guid userId, bool keepsThirdPartiesOut)
        => cache.Set(CacheKey(userId), keepsThirdPartiesOut, RememberedFor);

    /// <summary>
    /// Drops what this instance remembers. Used when told by another instance, which has already made
    /// the change itself - announcing it onwards from here would bounce it around the deployment.
    /// </summary>
    public void Forget(Guid userId) => cache.Remove(CacheKey(userId));

    /// <summary>
    /// Drops it here and tells the others to do the same, so the next request lands on the new answer
    /// whichever replica it reaches.
    ///
    /// The notice is best-effort, like everything on that bus. If it cannot be sent, the other instances
    /// fall back to expiring the entry within the minute - which is the behaviour this replaces rather
    /// than a new failure, and is why a request that has already saved the choice is not failed over it.
    /// </summary>
    public async Task ForgetEverywhereAsync(Guid userId, CancellationToken cancellationToken)
    {
        Forget(userId);
        await notices.SendAsync(Channel, JsonSerializer.Serialize(userId), cancellationToken);
    }

    private static string CacheKey(Guid userId) => $"orbit.privacy.{userId}";
}
