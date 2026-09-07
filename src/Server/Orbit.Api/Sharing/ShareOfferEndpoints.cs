using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using Orbit.Contracts.Sharing;
using Orbit.Core.Abstractions;
using Orbit.Core.Notifications;
using Orbit.Core.Sharing.GetShareOffer;

namespace Orbit.Api.Sharing;

/// <summary>
/// One offer, read by the person it was made to - what the page a share's notification leads to shows
/// (see Orbit.Web's ShareInvitation.razor).
///
/// One endpoint for all four kinds rather than a fifth on each of the four sections: the kind is in the
/// path, the query dispatches on it, and a client that has one address to call cannot call three of
/// them wrongly. Accepting stays where it already is - each section's own
/// <c>shares/{id}/accept</c> - because those carry the rules for that kind (a task list's share, for
/// one, drags the lists it gathers along with it).
/// </summary>
public static class ShareOfferEndpoints
{
    public static void MapShareOfferEndpoints(this WebApplication app)
    {
        // Every read here is scoped to the caller: an offer made to somebody else is not there.
        var offers = app.MapGroup("/api/shares").RequireAuthorization();

        offers.MapGet("/{kind}/{shareId:guid}", async (
            string kind, Guid shareId, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            if (KindOf(kind) is not { } sharedItemKind)
            {
                // A kind this build does not know, which is a client newer than the server - answered
                // as "no such offer" rather than as a bad request: the reader is not at fault and the
                // page says the same thing for both.
                return Results.NotFound();
            }

            var offer = await dispatcher.SendAsync(
                new GetShareOfferQuery(GetUserId(user), sharedItemKind, shareId), cancellationToken);

            return offer is null
                ? Results.NotFound()
                : Results.Ok(new ShareOfferDto(offer.ItemId, offer.ItemTitle, offer.IsAccepted));
        });
    }

    /// <summary>
    /// The names SharedItemNotifier writes into a notification's path. Stable: they sit in rows already
    /// written and in paths already handed to a phone, so renaming one orphans every invitation that
    /// carried it.
    /// </summary>
    private static SharedItemKind? KindOf(string kind) => kind.ToLowerInvariant() switch
    {
        "note" => SharedItemKind.Note,
        "tasklist" => SharedItemKind.TaskList,
        "event" => SharedItemKind.CalendarEvent,
        "inventory" => SharedItemKind.Inventory,
        _ => null
    };

    private static Guid GetUserId(ClaimsPrincipal user)
    {
        var subject = user.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? throw new InvalidOperationException("Authenticated request is missing a 'sub' claim.");
        return Guid.Parse(subject);
    }
}
