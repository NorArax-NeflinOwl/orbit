using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Orbit.Contracts.Inventory;
using Orbit.Core.Abstractions;
using Orbit.Core.Inventory;
using Orbit.Core.Inventory.CreateInventoryItem;
using Orbit.Core.Inventory.DeleteInventoryItem;
using Orbit.Core.Inventory.GetInventoryItemById;
using Orbit.Core.Inventory.GetInventoryItems;
using Orbit.Core.Inventory.UpdateInventoryItem;
using Orbit.Core.Notifications;

namespace Orbit.Api.Inventory;

public static class InventoryEndpoints
{
    public static void MapInventoryEndpoints(this WebApplication app)
    {
        // Every inventory item belongs to exactly one user (see GetUserId below), so the whole group
        // requires a valid, authenticated caller.
        var inventory = app.MapGroup("/api/inventory").RequireAuthorization();

        inventory.MapGet("/", async (ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(new GetInventoryItemsQuery(GetUserId(user)), cancellationToken);
            return Results.Ok(result.Select(ToDto));
        });

        inventory.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var item = await dispatcher.SendAsync(new GetInventoryItemByIdQuery(GetUserId(user), id), cancellationToken);
            return item is null ? Results.NotFound() : Results.Ok(ToDto(item));
        });

        inventory.MapPost("/", async (
            CreateInventoryItemRequest request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var id = await dispatcher.SendAsync(
                new CreateInventoryItemCommand(
                    GetUserId(user), request.Name, request.ProductType, request.Category, request.Quantity, request.MinimumQuantity,
                    request.ExpiryDate, Enum.Parse<NotificationChannel>(request.ExpiryNotificationChannel, ignoreCase: true)),
                cancellationToken);
            return Results.Created($"/api/inventory/{id}", id);
        });

        inventory.MapPut("/{id:guid}", async (
            Guid id, UpdateInventoryItemRequest request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var outcome = await dispatcher.SendAsync(
                new UpdateInventoryItemCommand(
                    GetUserId(user), id, request.Name, request.ProductType, request.Category, request.Quantity, request.MinimumQuantity,
                    request.ExpiryDate, Enum.Parse<NotificationChannel>(request.ExpiryNotificationChannel, ignoreCase: true)),
                cancellationToken);
            return ToApiResult(outcome);
        });

        inventory.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var deleted = await dispatcher.SendAsync(new DeleteInventoryItemCommand(GetUserId(user), id), cancellationToken);
            return deleted ? Results.NoContent() : Results.NotFound();
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

    private static InventoryItemDto ToDto(InventoryItem item)
        => new(
            item.Id, item.Name, item.ProductType, item.Category, item.Quantity, item.MinimumQuantity, item.ExpiryDate,
            item.ExpiryNotificationChannel.ToString(), item.IsBelowMinimum, item.PendingRestockTaskItemId is not null,
            item.CreatedAtUtc, item.UpdatedAtUtc);

    /// <summary>Maps an EditOutcome onto the corresponding HTTP response - Inventory has no locking concept, so Locked never actually occurs here.</summary>
    private static IResult ToApiResult(EditOutcome outcome)
        => outcome.Kind == EditOutcomeKind.Success ? Results.NoContent() : Results.NotFound();
}
