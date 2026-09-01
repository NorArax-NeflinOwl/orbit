using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Orbit.Contracts.Config;
using Orbit.Core;
using Orbit.Core.Mobile;
using Orbit.Core.Permissions;
using Orbit.GoogleIntegration;

namespace Orbit.Api.Config;

public static class ConfigEndpoints
{
    /// <summary>
    /// Server-environment flags the Blazor client can't determine on its own (it has no equivalent of
    /// IWebHostEnvironment) - unauthenticated, fetched once at app start, same shape as
    /// PushSubscriptionEndpoints' public-key endpoint.
    /// </summary>
    public static void MapConfigEndpoints(this WebApplication app)
    {
        app.MapGet("/api/config/client-flags", (
            IWebHostEnvironment environment, IOptionsMonitor<GoogleAuthSettings> googleAuthSettings,
            IConfiguration configuration) =>
            Results.Ok(new ClientFlagsDto(
                environment.IsDevelopment(),
                googleAuthSettings.CurrentValue.ClientId,
                // The first configured web origin - the same list CORS is built from, so a deployment
                // that lets the browser client talk to this server has already said where it lives.
                (configuration["WebClientOrigins"] ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .FirstOrDefault() ?? string.Empty,
                googleAuthSettings.CurrentValue.AndroidClientId,
                googleAuthSettings.CurrentValue.IosClientId)));

        // Which build of the server this is, for the client's footer and the phone's About row. The two
        // can differ - see ServerVersionDto - so a client showing only its own version answers "which
        // Orbit is this" with half the truth.
        //
        // Unauthenticated like the rest of this group: it is the answer to "what am I talking to", which
        // is exactly the question somebody has when they cannot sign in.
        app.MapGet("/api/config/version", async (
            ClaimsPrincipal user, IUserPermissionRepository userPermissionRepository, CancellationToken cancellationToken) =>
        {
            var version = OrbitVersion.ReadFrom(typeof(ConfigEndpoints).Assembly);
            // The hash is left out of the answer rather than sent and hidden by whoever asked: what is
            // not sent cannot be read off the wire. The number still goes to everybody, which is what
            // keeps this endpoint open - see below.
            return Results.Ok(new ServerVersionDto(
                version.Version,
                await MaySeeTheCommitAsync(user, userPermissionRepository, cancellationToken)
                    ? version.CommitHash
                    : string.Empty));
        });

        // Deliberately unauthenticated, like the endpoint above: a build too old to sign in still has to
        // be able to find out that it must update. The app caches the answer so it can decide offline -
        // see MobileVersionVerdictDto.
        app.MapGet("/api/config/mobile-version", (
            string platform, string? version, IOptionsMonitor<MobileVersionSettings> settings) =>
        {
            if (!Enum.TryParse<MobilePlatform>(platform, ignoreCase: true, out var mobilePlatform))
            {
                return Results.BadRequest(new { message = $"Unknown platform '{platform}'." });
            }

            var platformSettings = settings.CurrentValue.For(mobilePlatform);
            var verdict = platformSettings.ToPolicy().Decide(version);
            return Results.Ok(new MobileVersionVerdictDto(
                verdict.ToString(),
                NullWhenEmpty(platformSettings.LatestVersion),
                NullWhenEmpty(platformSettings.UpdateUrl)));
        });
    }

    /// <summary>
    /// Whether this caller is one of the accounts shown Orbit's own internals - see
    /// <see cref="ApplicationPermission.Debug"/>. Read from what the account holds rather than from the
    /// token, so a permission taken away takes effect on the next request.
    ///
    /// Anonymous callers are not, and the endpoint stays open to them anyway: which build a server is
    /// running is the answer to "what am I talking to", and a client too old to sign in still has to be
    /// able to read it. The number answers that; the commit is the extra detail that does not.
    /// </summary>
    private static async Task<bool> MaySeeTheCommitAsync(
        ClaimsPrincipal user, IUserPermissionRepository userPermissionRepository, CancellationToken cancellationToken)
    {
        if (user.FindFirstValue(JwtRegisteredClaimNames.Sub) is not { } subject
            || !Guid.TryParse(subject, out var userId))
        {
            return false;
        }

        var granted = await userPermissionRepository.GetForUserAsync(userId, cancellationToken);
        return ApplicationPermission.Debug.IsEffective(granted);
    }

    private static string? NullWhenEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
