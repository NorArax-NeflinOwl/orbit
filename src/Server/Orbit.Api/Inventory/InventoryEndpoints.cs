using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Orbit.Api.Permissions;
using Orbit.Contracts;
using Orbit.Contracts.Inventory;
using Orbit.Contracts.Sharing;
using Orbit.Core.Abstractions;
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

        warehouses.MapGet("/{warehouseId:guid}", async (
            Guid warehouseId, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var callerId = GetUserId(user);
            var warehouse = await dispatcher.SendAsync(new GetWarehouseByIdQuery(callerId, warehouseId), cancellationToken);
            return warehouse is null ? Results.NotFound() : Results.Ok(ToDto(warehouse, callerId));
        });

        // Creating a warehouse takes its name, not its contents - items are created through the save
        // below, exactly as task items are through their task list. Anything sent here would have been
        // dropped without a word, leaving the caller holding a warehouse that quietly lost what it was
        // told to keep, so it is refused instead.
        warehouses.MapPost("/", async (
            SaveWarehouseRequest request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            if (request.Items.Count > 0)
            {
                throw new InvalidRequestException(
                    "A warehouse is created with a name and filled afterwards - send its items to PUT /api/warehouses/{id} instead.");
            }

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
            ToDto(warehouse.EncryptedContent));


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
            // 403 rather than 404: the caller can see this, so hiding it from them now would only confuse.
            EditOutcomeKind.ReadOnly => Results.Json(
                new RefusalDto("This was shared with you to read, not to change."), statusCode: StatusCodes.Status403Forbidden),
            _ => Results.NotFound()
        };
}
