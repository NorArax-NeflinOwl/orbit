using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Orbit.Contracts.Notifications;
using Orbit.Core.Abstractions;
using Orbit.Core.Notifications;
using Orbit.Core.Notifications.GetNotificationEntries;
using Orbit.Core.Notifications.GetNotificationSettings;
using Orbit.Core.Notifications.GetUnreadNotificationCount;
using Orbit.Core.Notifications.MarkAllNotificationsRead;
using Orbit.Core.Notifications.UpdateNotificationSettings;

namespace Orbit.Api.Notifications;

public static class NotificationEndpoints
{
    /// <summary>Capped so the panel never has to render or transfer an unbounded feed - matches the "recent" framing in the UI, not a full history.</summary>
    private const int MaxRecentEntries = 30;

    public static void MapNotificationEndpoints(this WebApplication app)
    {
        var notifications = app.MapGroup("/api/notifications").RequireAuthorization();

        notifications.MapGet("/settings", async (ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var settings = await dispatcher.SendAsync(new GetNotificationSettingsQuery(GetUserId(user)), cancellationToken);
            return Results.Ok(ToDto(settings));
        });

        notifications.MapPut("/settings", async (
            UpdateNotificationSettingsRequest request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var settings = await dispatcher.SendAsync(
                new UpdateNotificationSettingsCommand(
                    GetUserId(user), request.AllowNotifications, request.AllowPush, request.AllowEmail,
                    request.AllowMobileBanner, request.ShowExceptionDetails),
                cancellationToken);
            return Results.Ok(ToDto(settings));
        });

        notifications.MapGet("/", async (ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var entries = await dispatcher.SendAsync(
                new GetNotificationEntriesQuery(GetUserId(user), MaxRecentEntries), cancellationToken);
            return Results.Ok(entries.Select(ToDto));
        });

        notifications.MapGet("/unread-count", async (ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var count = await dispatcher.SendAsync(new GetUnreadNotificationCountQuery(GetUserId(user)), cancellationToken);
            return Results.Ok(new UnreadCountDto(count));
        });

        notifications.MapPost("/read", async (ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            await dispatcher.SendAsync(new MarkAllNotificationsReadCommand(GetUserId(user)), cancellationToken);
            return Results.NoContent();
        });
    }

    /// <summary>
    /// Reads the authenticated user's id out of the JWT's "sub" claim. Safe to assume it's present and
    /// valid: the group requires authorization, and Orbit.Api only ever issues tokens with this claim
    /// (see TokenService).
    /// </summary>
    private static Guid GetUserId(ClaimsPrincipal user)
    {
        var subject = user.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? throw new InvalidOperationException("Authenticated request is missing a 'sub' claim.");
        return Guid.Parse(subject);
    }

    private static NotificationSettingsDto ToDto(NotificationSettings settings)
        => new(settings.AllowNotifications, settings.AllowPush, settings.AllowEmail, settings.AllowMobileBanner, settings.ShowExceptionDetails);

    private static NotificationEntryDto ToDto(NotificationEntry entry)
        => new(entry.Id, entry.Kind.ToString(), entry.Title, entry.Body, entry.Url, entry.CreatedAtUtc, entry.IsRead);
}
