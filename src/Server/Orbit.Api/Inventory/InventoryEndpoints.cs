using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Orbit.Contracts.Inventory;
using Orbit.Contracts.Sharing;
using Orbit.Core.Abstractions;
using Orbit.Core.Inventory;
using Orbit.Core.Inventory.AcceptWarehouseShare;
using Orbit.Core.Inventory.CreateInventoryItem;
using Orbit.Core.Inventory.CreateWarehouse;
using Orbit.Core.Inventory.DeleteInventoryItem;
using Orbit.Core.Inventory.DeleteWarehouse;
using Orbit.Core.Inventory.GetInventoryItemById;
using Orbit.Core.Inventory.GetInventoryItems;
using Orbit.Core.Inventory.GetWarehouseById;
using Orbit.Core.Inventory.GetWarehouses;
using Orbit.Core.Inventory.GetWarehouseShareStatus;
using Orbit.Core.Inventory.ShareWarehouse;
using Orbit.Core.Inventory.UpdateInventoryItem;
using Orbit.Core.Inventory.UpdateWarehouse;
using Orbit.Core.Notifications;

namespace Orbit.Api.Inventory;

public static class InventoryEndpoints
{
    public static void MapInventoryEndpoints(this WebApplication app)
    {
        // Items live under their warehouse, because that's what decides who may see or change them (see
        // WarehouseAccessResolver) - there is no route that reaches an item without naming its warehouse.
        var warehouses = app.MapGroup("/api/warehouses").RequireAuthorization();

        warehouses.MapGet("/", async (ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(new GetWarehousesQuery(GetUserId(user)), cancellationToken);
            return Results.Ok(result.Select(ToDto));
        });

        warehouses.MapGet("/{warehouseId:guid}", async (
            Guid warehouseId, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var warehouse = await dispatcher.SendAsync(new GetWarehouseByIdQuery(GetUserId(user), warehouseId), cancellationToken);
            return warehouse is null ? Results.NotFound() : Results.Ok(ToDto(warehouse));
        });

        warehouses.MapPost("/", async (
            SaveWarehouseRequest request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var id = await dispatcher.SendAsync(new CreateWarehouseCommand(GetUserId(user), request.Name), cancellationToken);
            return Results.Created($"/api/warehouses/{id}", id);
        });

        warehouses.MapPut("/{warehouseId:guid}", async (
            Guid warehouseId, SaveWarehouseRequest request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var outcome = await dispatcher.SendAsync(
                new UpdateWarehouseCommand(GetUserId(user), warehouseId, request.Name), cancellationToken);
            return ToApiResult(outcome);
        });

        warehouses.MapDelete("/{warehouseId:guid}", async (
            Guid warehouseId, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var deleted = await dispatcher.SendAsync(new DeleteWarehouseCommand(GetUserId(user), warehouseId), cancellationToken);
            return deleted ? Results.NoContent() : Results.NotFound();
        });

        warehouses.MapPost("/{warehouseId:guid}/shares", async (
            Guid warehouseId, ShareWarehouseRequest request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var outcome = await dispatcher.SendAsync(
                new ShareWarehouseCommand(
                    GetUserId(user), warehouseId, request.RecipientUserId,
                    Enum.Parse<ShareAccessLevel>(request.AccessLevel, ignoreCase: true)),
                cancellationToken);
            return outcome is null ? Results.NotFound() : Results.Ok(new ShareResultDto(outcome.ShareId, outcome.AlreadyShared));
        });

        warehouses.MapPost("/shares/{shareId:guid}/accept", async (
            Guid shareId, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var accepted = await dispatcher.SendAsync(new AcceptWarehouseShareCommand(GetUserId(user), shareId), cancellationToken);
            return accepted ? Results.NoContent() : Results.NotFound();
        });

        // Lets Chat.razor show an accurate "Accept" vs. "already accepted" state for a warehouse-share
        // message even after a page reload, instead of only remembering what was clicked this session.
        warehouses.MapGet("/shares/{shareId:guid}/status", async (
            Guid shareId, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var isAccepted = await dispatcher.SendAsync(new GetWarehouseShareStatusQuery(GetUserId(user), shareId), cancellationToken);
            return isAccepted is null ? Results.NotFound() : Results.Ok(isAccepted);
        });

        warehouses.MapGet("/{warehouseId:guid}/items", async (
            Guid warehouseId, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(new GetInventoryItemsQuery(GetUserId(user), warehouseId), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result.Select(ToDto));
        });

        warehouses.MapGet("/{warehouseId:guid}/items/{id:guid}", async (
            Guid warehouseId, Guid id, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var item = await dispatcher.SendAsync(new GetInventoryItemByIdQuery(GetUserId(user), warehouseId, id), cancellationToken);
            return item is null ? Results.NotFound() : Results.Ok(ToDto(item));
        });

        warehouses.MapPost("/{warehouseId:guid}/items", async (
            Guid warehouseId, CreateInventoryItemRequest request, ClaimsPrincipal user, IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var id = await dispatcher.SendAsync(
                new CreateInventoryItemCommand(
                    GetUserId(user), warehouseId, request.Name, request.ProductType, request.Category, request.Quantity,
                    request.MinimumQuantity, request.ExpiryDate,
                    Enum.Parse<NotificationChannel>(request.ExpiryNotificationChannel, ignoreCase: true)),
                cancellationToken);
            return id is null ? Results.NotFound() : Results.Created($"/api/warehouses/{warehouseId}/items/{id}", id);
        });

        warehouses.MapPut("/{warehouseId:guid}/items/{id:guid}", async (
            Guid warehouseId, Guid id, UpdateInventoryItemRequest request, ClaimsPrincipal user, IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var outcome = await dispatcher.SendAsync(
                new UpdateInventoryItemCommand(
                    GetUserId(user), warehouseId, id, request.Name, request.ProductType, request.Category, request.Quantity,
                    request.MinimumQuantity, request.ExpiryDate,
                    Enum.Parse<NotificationChannel>(request.ExpiryNotificationChannel, ignoreCase: true)),
                cancellationToken);
            return ToApiResult(outcome);
        });

        warehouses.MapDelete("/{warehouseId:guid}/items/{id:guid}", async (
            Guid warehouseId, Guid id, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var deleted = await dispatcher.SendAsync(
                new DeleteInventoryItemCommand(GetUserId(user), warehouseId, id), cancellationToken);
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

    private static WarehouseDto ToDto(Warehouse warehouse)
        => new(
            warehouse.Id, warehouse.Name, warehouse.CreatedAtUtc, warehouse.UpdatedAtUtc,
            warehouse.IsShared, warehouse.SharedByUserName, warehouse.AccessLevel.ToString());

    private static InventoryItemDto ToDto(InventoryItem item)
        => new(
            item.Id, item.Name, item.ProductType, item.Category, item.Quantity, item.MinimumQuantity, item.ExpiryDate,
            item.ExpiryNotificationChannel.ToString(), item.IsBelowMinimum, item.PendingRestockTaskItemId is not null,
            item.CreatedAtUtc, item.UpdatedAtUtc);

    /// <summary>Maps an EditOutcome onto the corresponding HTTP response - Inventory has no locking concept, so Locked never actually occurs here.</summary>
    private static IResult ToApiResult(EditOutcome outcome)
        => outcome.Kind == EditOutcomeKind.Success ? Results.NoContent() : Results.NotFound();
}
