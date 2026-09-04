using System.Diagnostics;
using System.Security.Claims;
using Microsoft.Extensions.Caching.Memory;
using Orbit.Core.Users;

namespace Orbit.Api.Telemetry;

/// <summary>
/// Keeps an account that asked to be left out of the trace this deployment keeps - the footer's
/// "Do not share my personal information", which on the server means Application Insights.
///
/// It clears the Recorded flag on the request's own activity rather than filtering at the exporter,
/// because the exporter is the wrong place to know who is calling: an activity starts before
/// authentication has run, so the only moment both facts exist is here, after UseAuthentication and
/// before the endpoint. An activity that is not Recorded is never exported, and everything started
/// under it inherits that.
///
/// The choice is cached for a minute per account. Without it this would be a database read on every
/// request in Orbit, which is a high price for a flag almost nobody sets; with it, turning the switch
/// on takes effect within a minute rather than instantly, which is the right trade for a standing
/// instruction rather than an action.
/// </summary>
public static class TraceOptOut
{
    private static readonly TimeSpan RememberedFor = TimeSpan.FromMinutes(1);

    public static IApplicationBuilder UseTraceOptOut(this IApplicationBuilder app)
        => app.Use(async (context, next) =>
        {
            if (Activity.Current is { } activity && context.User.FindFirstValue("sub") is { } subject
                && Guid.TryParse(subject, out var userId)
                && await KeepsThirdPartiesOutAsync(context, userId))
            {
                activity.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
                activity.IsAllDataRequested = false;
            }

            await next();
        });

    private static async Task<bool> KeepsThirdPartiesOutAsync(HttpContext context, Guid userId)
    {
        var cache = context.RequestServices.GetRequiredService<IMemoryCache>();
        if (cache.TryGetValue<bool>(CacheKey(userId), out var remembered))
        {
            return remembered;
        }

        var users = context.RequestServices.GetRequiredService<IUserRepository>();
        var user = await users.GetByIdAsync(userId, context.RequestAborted);
        var keepsThemOut = user?.KeepsThirdPartiesOut ?? false;
        cache.Set(CacheKey(userId), keepsThemOut, RememberedFor);
        return keepsThemOut;
    }

    /// <summary>
    /// Cleared when the choice changes, so turning the switch *off* is not left waiting out a minute of
    /// nothing being recorded. Turning it on is left to expire: a minute more of trace for somebody who
    /// has just asked for none is the wrong way round, but re-reading on every request to shave it is
    /// the price this cache exists to avoid, and the endpoint clearing it is what makes both immediate.
    /// </summary>
    public static void Forget(IMemoryCache cache, Guid userId) => cache.Remove(CacheKey(userId));

    private static string CacheKey(Guid userId) => $"orbit.privacy.{userId}";
}
