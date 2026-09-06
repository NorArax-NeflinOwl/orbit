using System.Diagnostics;
using System.Security.Claims;
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
/// The choice is cached for a minute per account - see <see cref="PrivacyChoiceCache"/>, which also
/// owns clearing it on every instance when it changes. Without the cache this would be a database read
/// on every request in Orbit, which is a high price for a flag almost nobody sets.
/// </summary>
public static class TraceOptOut
{
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
        var choices = context.RequestServices.GetRequiredService<PrivacyChoiceCache>();
        if (choices.TryRecall(userId, out var remembered))
        {
            return remembered;
        }

        var users = context.RequestServices.GetRequiredService<IUserRepository>();
        var user = await users.GetByIdAsync(userId, context.RequestAborted);
        var keepsThemOut = user?.KeepsThirdPartiesOut ?? false;
        choices.Remember(userId, keepsThemOut);
        return keepsThemOut;
    }
}
