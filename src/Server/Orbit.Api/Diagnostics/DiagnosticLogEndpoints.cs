using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Orbit.Contracts.Diagnostics;
using Orbit.Core.Diagnostics;

namespace Orbit.Api.Diagnostics;

public static class DiagnosticLogEndpoints
{
    public static void MapDiagnosticLogEndpoints(this WebApplication app)
    {
        // Authenticated: an entry is stored against the account that sent it, and an anonymous write
        // endpoint taking arbitrary text is not something worth having. Rate-limited because it accepts
        // a large body, and the same policy the auth endpoints use is the strictest one configured.
        var diagnostics = app.MapGroup("/api/diagnostics").RequireAuthorization();

        diagnostics.MapPost("/logs", async (
            UploadDiagnosticLogRequest request, ClaimsPrincipal user, IDiagnosticLogRepository repository,
            IOptionsMonitor<DiagnosticLogSettings> settings, CancellationToken cancellationToken) =>
        {
            var entries = DiagnosticLogParser.Parse(request.FileContent);
            if (entries.Count == 0)
            {
                // Nothing readable in the file. Not an error - a log from a phone that was already
                // failing is often truncated - but worth telling the app so it doesn't claim success.
                return Results.Ok(new UploadDiagnosticLogResponse(StoredEntryCount: 0));
            }

            var device = new MobileDeviceInfo(
                request.AppVersion, request.Platform, request.OperatingSystemVersion, request.DeviceModel).Truncated();
            var receivedAtUtc = DateTimeOffset.UtcNow;

            await repository.AddAsync(GetUserId(user), device, entries, receivedAtUtc, cancellationToken);
            // Swept here as well as hourly, so a report never lands beside entries already past the
            // window - see DiagnosticLogRetentionBackgroundService for why hourly is the one that counts.
            await repository.DeleteReceivedBeforeAsync(
                receivedAtUtc - TimeSpan.FromDays(settings.CurrentValue.RetentionDays), cancellationToken);

            return Results.Ok(new UploadDiagnosticLogResponse(entries.Count));
        }).RequireRateLimiting(RateLimiterPolicyNames.Auth);
    }

    /// <summary>Mirrors UserEndpoints.GetUserId - the group requires authorization, so the claim is present.</summary>
    private static Guid GetUserId(ClaimsPrincipal user)
    {
        var subject = user.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? throw new InvalidOperationException("Authenticated request is missing a 'sub' claim.");
        return Guid.Parse(subject);
    }
}
