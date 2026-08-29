using Orbit.Core.Mobile;
using Orbit.Mobile.Update;

namespace Orbit.Maui.Platform;

/// <summary>
/// Remembers the last version verdict in <see cref="IPreferences"/>. Deliberately not the secure store:
/// this is a public fact about a released build, and the Keychain is for things that would matter if
/// they leaked.
///
/// Stored as loose keys rather than JSON because it is read on the startup path, before the splash
/// screen releases, and there is nothing here whose parts have to move together.
/// </summary>
public sealed class PreferencesVersionVerdictCache : IVersionVerdictCache
{
    private const string VersionKey = "orbit.version-gate.version";
    private const string VerdictKey = "orbit.version-gate.verdict";
    private const string LatestVersionKey = "orbit.version-gate.latest-version";
    private const string UpdateUrlKey = "orbit.version-gate.update-url";

    private readonly IPreferences _preferences;

    public PreferencesVersionVerdictCache(IPreferences preferences) => _preferences = preferences;

    public Task<CachedVersionVerdict?> ReadAsync(CancellationToken cancellationToken)
    {
        var version = _preferences.Get<string?>(VersionKey, null);
        if (string.IsNullOrEmpty(version)
            || !Enum.TryParse<MobileVersionVerdict>(_preferences.Get<string?>(VerdictKey, null), out var verdict))
        {
            return Task.FromResult<CachedVersionVerdict?>(null);
        }

        return Task.FromResult<CachedVersionVerdict?>(new CachedVersionVerdict(
            version, verdict, _preferences.Get<string?>(LatestVersionKey, null), _preferences.Get<string?>(UpdateUrlKey, null)));
    }

    public Task WriteAsync(CachedVersionVerdict verdict, CancellationToken cancellationToken)
    {
        _preferences.Set(VersionKey, verdict.DisplayVersion);
        _preferences.Set(VerdictKey, verdict.Verdict.ToString());
        _preferences.Set(LatestVersionKey, verdict.LatestVersion);
        _preferences.Set(UpdateUrlKey, verdict.UpdateUrl);
        return Task.CompletedTask;
    }
}
