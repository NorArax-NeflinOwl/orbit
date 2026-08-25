using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using Orbit.Contracts.Sharing;
using Orbit.Core.Abstractions;
using Orbit.Core.Sharing;
using Orbit.Core.Sharing.ClaimPublicShareLink;
using Orbit.Core.Sharing.CreatePublicShareLink;
using Orbit.Core.Sharing.GetPublicSharedItem;
using Orbit.Core.Sharing.RevokePublicShareLink;

namespace Orbit.Api.Sharing;

/// <summary>
/// Two groups on purpose. /api/public/{token} is the only unauthenticated read in the whole API - the
/// token is the access check - and is rate limited, since an endpoint that answers "does this token
/// exist" to anyone is the one place guessing is even worth attempting. Everything that makes or
/// withdraws a link requires the owner's own session, like every other endpoint.
/// </summary>
public static class PublicShareEndpoints
{
    public static void MapPublicShareEndpoints(this WebApplication app)
    {
        var links = app.MapGroup("/api/share-links").RequireAuthorization();

        links.MapPost("/", async (
            CreatePublicShareLinkRequest request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var itemType = RequestEnum.Parse<SharedItemType>(request.ItemType, nameof(request.ItemType));
            var link = await dispatcher.SendAsync(
                new CreatePublicShareLinkCommand(GetUserId(user), itemType, request.ItemId), cancellationToken);

            return link is null
                ? Results.NotFound()
                : Results.Ok(new PublicShareLinkDto(link.Token, link.CreatedAtUtc));
        });

        // A GET rather than part of the item's own payload: a link is only made when someone asks for
        // one, and this answers "is there one already" without making one.
        links.MapGet("/{itemType}/{itemId:guid}", async (
            string itemType, Guid itemId, ClaimsPrincipal user, IPublicShareLinkRepository repository, CancellationToken cancellationToken) =>
        {
            var parsedItemType = RequestEnum.Parse<SharedItemType>(itemType, nameof(itemType));
            var link = await repository.GetLiveForItemAsync(GetUserId(user), parsedItemType, itemId, cancellationToken);

            return link is null
                ? Results.NoContent()
                : Results.Ok(new PublicShareLinkDto(link.Token, link.CreatedAtUtc));
        });

        links.MapDelete("/{itemType}/{itemId:guid}", async (
            string itemType, Guid itemId, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var parsedItemType = RequestEnum.Parse<SharedItemType>(itemType, nameof(itemType));
            await dispatcher.SendAsync(new RevokePublicShareLinkCommand(GetUserId(user), parsedItemType, itemId), cancellationToken);
            return Results.NoContent();
        });

        var publicReads = app.MapGroup("/api/public").RequireRateLimiting(RateLimiterPolicyNames.PublicShare);

        publicReads.MapGet("/{token}", async (string token, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var item = await dispatcher.SendAsync(new GetPublicSharedItemQuery(token), cancellationToken);
            return item is null ? Results.NotFound() : Results.Ok(ToDto(item));
        });

        // The only authenticated endpoint in this group: saving a link's item into your own account is
        // something an account does, which is what the page's "sign in to save this" leads to.
        publicReads.MapPost("/{token}/claim", async (
            string token, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(new ClaimPublicShareLinkCommand(token, GetUserId(user)), cancellationToken);
            return result.Claimed || result.AlreadyHeld
                ? Results.Ok(new ClaimPublicShareLinkResponse(result.ItemType.ToString(), result.ItemId, result.AlreadyHeld))
                : Results.NotFound();
        }).RequireAuthorization();
    }

    private static PublicSharedItemDto ToDto(PublicSharedItem item)
        => new(
            item.ItemType.ToString(), item.Title, item.Subtitle,
            item.Lines.Select(line => new PublicSharedItemLineDto(line.Text, line.IsChecklistItem, line.IsChecked, line.Detail)).ToList(),
            item.OwnerDisplayName, item.UpdatedAtUtc);

    private static Guid GetUserId(ClaimsPrincipal user)
    {
        var subject = user.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? throw new InvalidOperationException("Authenticated request is missing a 'sub' claim.");
        return Guid.Parse(subject);
    }
}
