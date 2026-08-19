using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Orbit.Api.Notifications;
using Orbit.Contracts.PushNotifications;
using Orbit.Core.Abstractions;
using Orbit.Core.PushNotifications.SubscribeToPush;
using Orbit.Core.PushNotifications.UnsubscribeFromPush;

namespace Orbit.Api.PushNotifications;

public static class PushSubscriptionEndpoints
{
    public static void MapPushNotificationEndpoints(this WebApplication app)
    {
        // Subscribing, unsubscribing and even reading the public key all belong to a specific
        // signed-in browser session, so the whole group requires a valid, authenticated caller.
        var push = app.MapGroup("/api/push").RequireAuthorization();

        // Fetched by Orbit.Web right before it calls the Push API's subscribe() with this as the
        // applicationServerKey - see wwwroot/js/pushNotifications.js.
        push.MapGet("/public-key", (IOptionsMonitor<VapidSettings> vapidSettings) =>
            Results.Ok(new PushPublicKeyDto(vapidSettings.CurrentValue.PublicKeyBase64Url)));

        push.MapPost("/subscriptions", async (
            PushSubscriptionRequest request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            await dispatcher.SendAsync(
                new SubscribeToPushCommand(GetUserId(user), request.Endpoint, request.P256dhBase64, request.AuthBase64),
                cancellationToken);
            return Results.NoContent();
        });

        push.MapDelete("/subscriptions", async (
            string endpoint, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var removed = await dispatcher.SendAsync(new UnsubscribeFromPushCommand(GetUserId(user), endpoint), cancellationToken);
            return removed ? Results.NoContent() : Results.NotFound();
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
}
