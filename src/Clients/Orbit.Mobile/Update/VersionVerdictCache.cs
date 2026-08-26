using Orbit.Core.Mobile;

namespace Orbit.Mobile.Update;

/// <summary>
/// The last verdict the server gave, remembered against the version it was about.
///
/// Storing the version alongside it is the whole point: a verdict is only ever applied to the build it
/// was issued for, so an "update required" about yesterday's build cannot block the update that fixed
/// it - see MobileVersionGate.
/// </summary>
public sealed record CachedVersionVerdict(
    string DisplayVersion, MobileVersionVerdict Verdict, string? LatestVersion, string? UpdateUrl);

/// <summary>
/// Persists <see cref="CachedVersionVerdict"/> across launches so the gate can still decide with no
/// network. Deliberately not secure storage: this is a public fact about a released build, and the
/// Keychain is reserved for things that would matter if they leaked.
/// </summary>
public interface IVersionVerdictCache
{
    Task<CachedVersionVerdict?> ReadAsync(CancellationToken cancellationToken);

    Task WriteAsync(CachedVersionVerdict verdict, CancellationToken cancellationToken);
}
