using Microsoft.Extensions.Options;
using Orbit.Contracts.Config;
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
            IWebHostEnvironment environment, IOptionsMonitor<GoogleAuthSettings> googleAuthSettings) =>
            Results.Ok(new ClientFlagsDto(environment.IsDevelopment(), googleAuthSettings.CurrentValue.ClientId)));
    }
}
