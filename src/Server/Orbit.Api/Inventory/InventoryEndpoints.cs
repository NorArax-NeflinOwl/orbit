using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Orbit.Api.Permissions;
using Orbit.Contracts;
using Orbit.Contracts.Inventory;
using Orbit.Contracts.Sharing;
using Orbit.Core.Abstractions;
using Orbit.Api.Sync;
using Orbit.Core.Sync;
using Orbit.Core.Inventory;
using Orbit.Core.Inventory.AcceptWarehouseShare;
using Orbit.Core.Inventory.AcquireWarehouseLock;
using Orbit.Core.Inventory.CreateWarehouse;
using Orbit.Core.Inventory.DeleteWarehouse;
using Orbit.Core.Inventory.GetInventoryItems;
using Orbit.Core.Inventory.GetWarehouseById;
using Orbit.Core.Inventory.GetWarehouses;
using Orbit.Core.Inventory.GetWarehouseShareStatus;
using Orbit.Core.Inventory.ReleaseWarehouseLock;
using Orbit.Core.Inventory.ShareWarehouse;
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
            var callerId = GetUserId(user);
            var result = await dispatcher.SendAsync(new GetWarehousesQuery(callerId), cancellationToken);
            return Results.Ok(result.Select(warehouse => ToDto(warehouse, callerId)));
        });

        // What a client needs to catch up after being away - see ChangeFeedDto. A deleted warehouse
        // takes its items with it, so only the warehouse needs a tombstone.
        warehouses.MapGet("/changes", async (
            DateTimeOffset since, ClaimsPrincipal user, IDispatcher dispatcher,
            ISyncTombstoneRepository tombstones, CancellationToken cancellationToken) =>
        {
            var callerId = GetUserId(user);
            var cursor = ChangeFeed.StartCursor();
            // The cursor goes to the database rather than being applied to everything it returned.
            var all = await dispatcher.SendAsync(new GetWarehousesQuery(callerId, since), cancellationToken);
            var changed = all.Select(warehouse => ToDto(warehouse, callerId)).ToList();

            return Results.Ok(await ChangeFeed.BuildAsync(
                changed, cursor, callerId, SyncEntityType.Warehouse, since, tombstones, cancellationToken));
        });

        warehouses.MapGet("/{warehouseId:guid}", async (
            Guid warehouseId, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var callerId = GetUserId(user);
            var warehouse = await dispatcher.SendAsync(new GetWarehouseByIdQuery(callerId, warehouseId), cancellationToken);
            return warehouse is null ? Results.NotFound() : Results.Ok(ToDto(warehouse, callerId));
        });

        warehouses.MapPost("/", async (
            SaveWarehouseRequest request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var id = await dispatcher.SendAsync(new CreateWarehouseCommand(GetUserId(user), request.Name, request.IsPrivate, ToDomainPayload(request.EncryptedContent)), cancellationToken);
            return Results.Created($"/api/warehouses/{id}", id);
        });

        warehouses.MapPut("/{warehouseId:guid}", async (
            Guid warehouseId, SaveWarehouseRequest request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var outcome = await dispatcher.SendAsync(
                new UpdateWarehouseCommand(
                    GetUserId(user), warehouseId, request.Name, ToDomainItems(request.Items),
                    request.IsPrivate, ToDomainPayload(request.EncryptedContent)),
                cancellationToken);
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
                    RequestEnum.Parse<ShareAccessLevel>(request.AccessLevel, "accessLevel")),
                cancellationToken);
            return outcome is null ? Results.NotFound() : Results.Ok(new ShareResultDto(outcome.ShareId, outcome.AlreadyShared, outcome.AccessLevelRaised));
        }).RequireAuthorization(PermissionPolicies.Sharing);

        warehouses.MapPost("/shares/{shareId:guid}/accept", async (
            Guid shareId, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var accepted = await dispatcher.SendAsync(new AcceptWarehouseShareCommand(GetUserId(user), shareId), cancellationToken);
            return accepted ? Results.NoContent() : Results.NotFound();
        }).RequireAuthorization(PermissionPolicies.Sharing);

        // Lets Chat.razor show an accurate "Accept" vs. "already accepted" state for a warehouse-share
        // message even after a page reload, instead of only remembering what was clicked this session.
        warehouses.MapGet("/shares/{shareId:guid}/status", async (
            Guid shareId, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var isAccepted = await dispatcher.SendAsync(new GetWarehouseShareStatusQuery(GetUserId(user), shareId), cancellationToken);
            return isAccepted is null ? Results.NotFound() : Results.Ok(isAccepted);
        }).RequireAuthorization(PermissionPolicies.Sharing);

        warehouses.MapGet("/{warehouseId:guid}/items", async (
            Guid warehouseId, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(new GetInventoryItemsQuery(GetUserId(user), warehouseId), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result.Select(ToDto));
        });

        // Items have no routes of their own: they are created, changed, and removed through the warehouse
        // save above, exactly as task items are through their task list.
        warehouses.MapPost("/{warehouseId:guid}/lock", async (
            Guid warehouseId, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var outcome = await dispatcher.SendAsync(new AcquireWarehouseLockCommand(GetUserId(user), warehouseId), cancellationToken);
            return outcome.Kind switch
            {
                EditOutcomeKind.Success => Results.NoContent(),
                EditOutcomeKind.Locked => Results.Json(new LockConflictDto(outcome.LockedByUserName!), statusCode: StatusCodes.Status409Conflict),
                _ => Results.NotFound()
            };
        });

        warehouses.MapDelete("/{warehouseId:guid}/lock", async (
            Guid warehouseId, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var released = await dispatcher.SendAsync(new ReleaseWarehouseLockCommand(GetUserId(user), warehouseId), cancellationToken);
            return released ? Results.NoContent() : Results.NotFound();
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

    private static WarehouseDto ToDto(Warehouse warehouse, Guid callerId)
        => new(
            warehouse.Id, warehouse.Name, warehouse.CreatedAtUtc, warehouse.UpdatedAtUtc,
            warehouse.IsShared, warehouse.SharedByUserName, warehouse.AccessLevel.ToString(),
            // Only someone ELSE's live lock is worth telling the caller about: their own lock never
            // blocks them, and an expired one is free for the taking - see IsLockedByAnotherUser.
            warehouse.IsLockedByAnotherUser(callerId, DateTimeOffset.UtcNow) ? warehouse.LockedByUserName : null,
            warehouse.IsShared ? warehouse.UserId : null,
            warehouse.IsPrivate,
            ToDto(warehouse.EncryptedContent),
            warehouse.IsSharedWithOthers);


    /// <summary>Both halves travel together or not at all, so a request carrying only one is treated as carrying neither.</summary>
    private static EncryptedPayload? ToDomainPayload(EncryptedContentDto? encryptedContent)
        => encryptedContent is null ? null : new EncryptedPayload(encryptedContent.Ciphertext, encryptedContent.Nonce);

    private static EncryptedContentDto? ToDto(EncryptedPayload? encryptedContent)
        => encryptedContent is null ? null : new EncryptedContentDto(encryptedContent.Ciphertext, encryptedContent.Nonce);

    private static IReadOnlyList<WarehouseItemInput> ToDomainItems(IReadOnlyList<WarehouseItemDto> items)
        => items
            .Select(item => new WarehouseItemInput(
                item.Id, item.Name, item.ProductType, item.Category, item.Quantity, item.MinimumQuantity, item.ExpiryDate,
                RequestEnum.Parse<NotificationChannel>(item.ExpiryNotificationChannel, "expiryNotificationChannel")))
            .ToList();

    private static InventoryItemDto ToDto(InventoryItem item)
        => new(
            item.Id, item.Name, item.ProductType, item.Category, item.Quantity, item.MinimumQuantity, item.ExpiryDate,
            item.ExpiryNotificationChannel.ToString(), item.IsBelowMinimum, item.PendingRestockTaskItemId is not null,
            item.CreatedAtUtc, item.UpdatedAtUtc);

    private static IResult ToApiResult(EditOutcome outcome)
        => outcome.Kind switch
        {
            EditOutcomeKind.Success => Results.NoContent(),
            EditOutcomeKind.Locked => Results.Json(new LockConflictDto(outcome.LockedByUserName!), statusCode: StatusCodes.Status409Conflict),
            _ => Results.NotFound()
        };
}
