using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Orbit.Contracts.Config;
using Orbit.Core.Mobile;

namespace Orbit.Mobile.Update;

/// <summary>
/// Decides, before the splash screen releases, whether this build is still allowed to run - the client
/// half of info/orbit-maui-plan.md's "Forced update". A released app cannot be rolled back the way a web
/// deploy can, so when a release changes something the server can no longer support, refusing to run is
/// the only way to retire it.
///
/// The rule that matters is the offline one: <b>block only on a verdict this build actually holds,
/// never on a missing one.</b> A gate that stopped the app whenever the server was unreachable would
/// brick it on a train - precisely the situation offline support exists for. The accepted failure mode
/// is an app that should have been blocked running offline until it next reaches the server, which is
/// harmless because the server rejects whatever it cannot support anyway.
/// </summary>
public sealed class MobileVersionGate
{
    /// <summary>
    /// The splash screen is held while this runs, so a slow or unreachable server must not hold it
    /// indefinitely - past this the app falls back to what it already knows.
    /// </summary>
    public static readonly TimeSpan ServerTimeout = TimeSpan.FromSeconds(5);

    private readonly AppVersion _appVersion;
    private readonly HttpClient _httpClient;
    private readonly IVersionVerdictCache _cache;
    private readonly ILogger<MobileVersionGate> _logger;

    public MobileVersionGate(
        AppVersion appVersion, HttpClient httpClient, IVersionVerdictCache cache,
        ILogger<MobileVersionGate> logger)
    {
        _appVersion = appVersion;
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
    }

    public async Task<VersionGateDecision> DecideAsync(CancellationToken cancellationToken = default)
    {
        if (await AskServerAsync(cancellationToken) is not { } fresh)
        {
            return await ReadRememberedDecisionAsync(cancellationToken);
        }

        await _cache.WriteAsync(
            new CachedVersionVerdict(_appVersion.DisplayVersion, fresh.Verdict, fresh.LatestVersion, fresh.UpdateUrl),
            cancellationToken);
        return fresh;
    }

    /// <summary>Null whenever the server did not produce a verdict this build can act on.</summary>
    private async Task<VersionGateDecision?> AskServerAsync(CancellationToken cancellationToken)
    {
        using var attempt = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        attempt.CancelAfter(ServerTimeout);

        try
        {
            var verdictDto = await _httpClient.GetFromJsonAsync<MobileVersionVerdictDto>(
                $"api/config/mobile-version?platform={_appVersion.Platform}" +
                $"&version={Uri.EscapeDataString(_appVersion.DisplayVersion)}",
                attempt.Token);

            if (verdictDto is null || !Enum.TryParse<MobileVersionVerdict>(verdictDto.Verdict, out var verdict))
            {
                // A verdict written by a newer server than this build understands. Treating it as no
                // answer at all falls back to what is already known, rather than guessing.
                _logger.LogWarning("Unrecognised version verdict '{Verdict}' from the server", verdictDto?.Verdict);
                return null;
            }

            return new VersionGateDecision(verdict, verdictDto.LatestVersion, verdictDto.UpdateUrl);
        }
        catch (Exception exception)
            when (exception is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            // Offline, or the server took longer than a splash screen should wait. Both are ordinary.
            _logger.LogInformation("Could not reach the version gate ({Reason}); using the last known verdict", exception.Message);
            return null;
        }
    }

    private async Task<VersionGateDecision> ReadRememberedDecisionAsync(CancellationToken cancellationToken)
    {
        var remembered = await _cache.ReadAsync(cancellationToken);
        if (remembered is null || remembered.DisplayVersion != _appVersion.DisplayVersion)
        {
            // Nothing is known about *this* build - either the app has never reached the server, or the
            // only verdict held is about a version that has since been replaced. Let it through.
            return VersionGateDecision.Supported;
        }

        return new VersionGateDecision(remembered.Verdict, remembered.LatestVersion, remembered.UpdateUrl);
    }
}
