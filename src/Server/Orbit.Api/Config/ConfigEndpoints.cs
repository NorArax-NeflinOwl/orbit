using Microsoft.Extensions.Options;
using Orbit.Contracts.Config;
using Orbit.Core.Mobile;
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
                    .FirstOrDefault() ?? string.Empty)));

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

    private static string? NullWhenEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
