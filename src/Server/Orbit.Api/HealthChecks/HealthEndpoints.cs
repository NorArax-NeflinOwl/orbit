using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Orbit.Api.HealthChecks;

public static class HealthEndpoints
{
    /// <summary>
    /// Maps three report endpoints tuned for different callers, plus one ad hoc single-service probe:
    /// - GET /health: full report, every check.
    /// - GET /health/ready: only checks tagged "ready" (dependencies); use for readiness probes.
    /// - GET /health/live: no checks at all; responding at all means the process is alive, so use for
    ///   liveness probes that shouldn't restart the process just because a dependency is down.
    /// - GET /health/services/{name}: probes one configured external service immediately, regardless
    ///   of its Enabled flag, without waiting for the next aggregated check run.
    /// </summary>
    public static void MapHealthEndpoints(this WebApplication app)
    {
        app.MapHealthChecks("/health", new HealthCheckOptions { ResponseWriter = WriteHealthReportAsync });

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains("ready"),
            ResponseWriter = WriteHealthReportAsync
        });

        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = WriteHealthReportAsync
        });

        app.MapGet("/health/services/{name}", CheckSingleExternalServiceAsync);
    }

    /// <summary>
    /// What the report says. <b>The full report at /health is published on the web origin</b> - nginx
    /// proxies it so the footer's Status link can reach it (see nginx-app-locations.conf), which means
    /// everything written here is readable by anyone, signed in or not.
    ///
    /// What that is today: each check's status and duration, which optional integrations are
    /// unconfigured and the *names* of the keys they are missing (never the values), free disk against
    /// its threshold, and the background services' heartbeats. All of it answers "is the server
    /// working", which is the question the link exists for.
    ///
    /// The one thing to know before adding to it: <c>external-services</c> puts each configured
    /// service's URL in its data, and that list is empty. Configuring one starts publishing its address
    /// - which may be exactly right for a public dependency and exactly wrong for anything else.
    /// </summary>
    // Internal rather than private so Orbit.Api.Tests can call these handlers directly (see the
    // InternalsVisibleTo entry in Orbit.Api.csproj) without going through a full HTTP round trip.
    internal static async Task WriteHealthReportAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description,
                durationMs = entry.Value.Duration.TotalMilliseconds,
                data = entry.Value.Data
            })
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }

    internal static async Task<IResult> CheckSingleExternalServiceAsync(
        string name,
        IOptionsMonitor<HealthCheckSettings> settings,
        ExternalServiceProber prober,
        CancellationToken cancellationToken)
    {
        var endpoint = settings.CurrentValue.ExternalServices.Services
            .FirstOrDefault(service => string.Equals(service.Name, name, StringComparison.OrdinalIgnoreCase));

        if (endpoint is null)
        {
            return Results.NotFound(new { message = $"No external service named '{name}' is configured." });
        }

        var result = await prober.ProbeAsync(endpoint, cancellationToken);

        return Results.Ok(new
        {
            result.Name,
            result.Url,
            result.IsHealthy,
            result.Description,
            result.DurationMs
        });
    }
}
