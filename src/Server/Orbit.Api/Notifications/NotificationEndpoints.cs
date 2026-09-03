using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Orbit.Contracts.Notifications;
using Orbit.Core.Abstractions;
using Orbit.Core.Notifications;
using Orbit.Core.Notifications.GetNotificationEntries;
using Orbit.Core.Notifications.GetNotificationSettings;
using Orbit.Core.Notifications.GetUnreadNotificationEntries;
using Orbit.Core.Notifications.ClearNotifications;
using Orbit.Api.Sync;
using Orbit.Core.Sync;
using Orbit.Core.Notifications.GetChangedNotifications;
using Orbit.Core.Notifications.GetNotificationHistory;
using Orbit.Core.Notifications.MarkNotificationsAtUrlRead;
using Orbit.Core.Notifications.MarkAllNotificationsRead;
using Orbit.Core.Notifications.UpdateNotificationSettings;

namespace Orbit.Api.Notifications;

public static class NotificationEndpoints
{
    /// <summary>Capped so the panel never has to render or transfer an unbounded feed - matches the "recent" framing in the UI, not a full history.</summary>
    private const int MaxRecentEntries = 30;

    /// <summary>Larger than the panel's cap: the notifications page is where someone goes to find one they cleared away.</summary>
    private const int MaxHistoryEntries = 200;

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
                    request.AllowMobileBanner, request.ShowExceptionDetails, request.AllowShareNotifications,
                    new BannerTiming(request.BannerVisibleSeconds, request.BannerMinimumGapSeconds), request.RetentionDays),
                cancellationToken);
            return Results.Ok(ToDto(settings));
        });

        notifications.MapGet("/", async (ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var entries = await dispatcher.SendAsync(
                new GetNotificationEntriesQuery(GetUserId(user), MaxRecentEntries), cancellationToken);
            return Results.Ok(entries.Select(ToDto));
        });

        // Everything still held, cleared entries included - the notifications page's own view. Clearing
        // the panel tidies entries away rather than destroying them; only the retention window deletes.
        notifications.MapGet("/history", async (ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var entries = await dispatcher.SendAsync(
                new GetNotificationHistoryQuery(GetUserId(user), MaxHistoryEntries), cancellationToken);
            return Results.Ok(entries.Select(ToDto));
        });

        // What has changed since a point in time, in the same shape the other four collections answer -
        // so a phone can hold its own copy of the feed and show it with no connection.
        //
        // No tombstones: an entry only ever leaves by outliving its retention window (see
        // DeleteExpiredAsync), which every client can work out for itself from the age of what it holds.
        // Writing one per expired notification would be a lot of rows to say something already known.
        notifications.MapGet("/changes", async (
            DateTimeOffset since, ClaimsPrincipal user, IDispatcher dispatcher,
            ISyncTombstoneRepository tombstones, CancellationToken cancellationToken) =>
        {
            var userId = GetUserId(user);
            var cursor = ChangeFeed.StartCursor();
            var changed = await dispatcher.SendAsync(
                new GetChangedNotificationsQuery(userId, since, MaxHistoryEntries), cancellationToken);

            return Results.Ok(await ChangeFeed.BuildAsync(
                changed.Select(ToDto).ToList(), cursor, userId, SyncEntityType.NotificationEntry, since,
                tombstones, cancellationToken));
        });

        // The unread entries themselves rather than a bare count: the client badges each place a
        // notification came from (a chat contact, a nav section) by reading their Url, and gets the
        // count for the avatar badge from the list length.
        notifications.MapGet("/unread", async (ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var entries = await dispatcher.SendAsync(
                new GetUnreadNotificationEntriesQuery(GetUserId(user), MaxRecentEntries), cancellationToken);
            return Results.Ok(entries.Select(ToDto));
        });

        notifications.MapDelete("/", async (ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            await dispatcher.SendAsync(new ClearNotificationsCommand(GetUserId(user)), cancellationToken);
            return Results.NoContent();
        });

        notifications.MapPost("/read", async (ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            await dispatcher.SendAsync(new MarkAllNotificationsReadCommand(GetUserId(user)), cancellationToken);
            return Results.NoContent();
        });

        // Arriving at the page a notification was about counts as having read it, however the reader got
        // there - see MarkNotificationsAtUrlReadCommand.
        notifications.MapPost("/read-at", async (
            MarkNotificationsReadAtUrlRequest request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            await dispatcher.SendAsync(new MarkNotificationsAtUrlReadCommand(GetUserId(user), request.Url), cancellationToken);
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
        => new(
            settings.AllowNotifications, settings.AllowPush, settings.AllowEmail, settings.AllowMobileBanner, settings.ShowExceptionDetails,
            settings.BannerTiming.VisibleSeconds, settings.BannerTiming.MinimumGapSeconds, settings.AllowShareNotifications,
            settings.RetentionDays);

    private static NotificationEntryDto ToDto(NotificationEntry entry)
        => new(
            entry.Id, entry.Kind.ToString(), entry.Title, entry.Body, entry.Url, entry.CreatedAtUtc,
            entry.IsRead, entry.IsDismissed, entry.TitleArguments, entry.BodyArguments);
}
