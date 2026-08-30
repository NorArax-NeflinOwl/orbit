using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Orbit.Contracts.Diagnostics;
using Orbit.Core.Diagnostics;

namespace Orbit.Api.Diagnostics;

public static class DiagnosticLogEndpoints
{
    /// <summary>
    /// The largest upload the endpoint will read. Derived from what the app can legitimately send rather
    /// than picked round: DiagnosticLogFile keeps two files of 256 KB each and ReadAll concatenates
    /// both, so half a megabyte of log text is the honest ceiling, and JSON escaping a line-oriented
    /// file adds a little on top of that.
    ///
    /// The rest is headroom, because this is a backstop against a caller that is not the app - the
    /// endpoint is authenticated and rate-limited, but it still takes a large body of arbitrary text,
    /// and without a cap that body is bounded only by ASP.NET's own default of about 30 MB. Four times
    /// the real ceiling leaves room for the file cap to grow without anyone remembering this number.
    /// </summary>
    private const long MaximumUploadBytes = 2 * 1024 * 1024;

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
        })
        .RequireRateLimiting(RateLimiterPolicyNames.Auth)
        .WithMetadata(new RequestSizeLimitAttribute(MaximumUploadBytes));
    }

    /// <summary>Mirrors UserEndpoints.GetUserId - the group requires authorization, so the claim is present.</summary>
    private static Guid GetUserId(ClaimsPrincipal user)
    {
        var subject = user.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? throw new InvalidOperationException("Authenticated request is missing a 'sub' claim.");
        return Guid.Parse(subject);
    }
}
