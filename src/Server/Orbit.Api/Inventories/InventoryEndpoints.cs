using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Orbit.Api.Permissions;
using Orbit.Contracts;
using Orbit.Contracts.Inventories;
using Orbit.Contracts.Sharing;
using Orbit.Core.Abstractions;
using Orbit.Api.Sync;
using Orbit.Core.Sync;
using Orbit.Core.Inventories;
using Orbit.Core.Inventories.RestockListSettingsAccess;
using Orbit.Core.Inventories.AcceptInventoryShare;
using Orbit.Core.Inventories.AcquireInventoryLock;
using Orbit.Core.Inventories.CreateInventory;
using Orbit.Core.Inventories.DeleteInventory;
using Orbit.Core.Inventories.GetInventoryItems;
using Orbit.Core.Inventories.GetInventoryById;
using Orbit.Core.Inventories.GetInventories;
using Orbit.Core.Inventories.GetInventoryShareStatus;
using Orbit.Core.Inventories.ReleaseInventoryLock;
using Orbit.Core.Inventories.ShareInventory;
using Orbit.Core.Inventories.UpdateInventory;
using Orbit.Core.Notifications;

namespace Orbit.Api.Inventories;

public static class InventoryEndpoints
{
    public static void MapInventoryEndpoints(this WebApplication app)
    {
        // Items live under their inventory, because that's what decides who may see or change them (see
        // InventoryAccessResolver) - there is no route that reaches an item without naming its inventory.
        var inventories = app.MapGroup("/api/inventories").RequireAuthorization();

        inventories.MapGet("/", async (ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var callerId = GetUserId(user);
            var result = await dispatcher.SendAsync(new GetInventoriesQuery(callerId), cancellationToken);
            return Results.Ok(result.Select(inventory => ToDto(inventory, callerId)));
        });

        // What a client needs to catch up after being away - see ChangeFeedDto. A deleted inventory
        // takes its items with it, so only the inventory needs a tombstone.
        inventories.MapGet("/changes", async (
            DateTimeOffset since, ClaimsPrincipal user, IDispatcher dispatcher,
            ISyncTombstoneRepository tombstones, CancellationToken cancellationToken) =>
        {
            var callerId = GetUserId(user);
            var cursor = ChangeFeed.StartCursor();
            // The cursor goes to the database rather than being applied to everything it returned.
            var all = await dispatcher.SendAsync(new GetInventoriesQuery(callerId, since), cancellationToken);
            var changed = all.Select(inventory => ToDto(inventory, callerId)).ToList();

            return Results.Ok(await ChangeFeed.BuildAsync(
                changed, cursor, callerId, SyncEntityType.Inventory, since, tombstones, cancellationToken));
        });

        inventories.MapGet("/{inventoryId:guid}", async (
            Guid inventoryId, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var callerId = GetUserId(user);
            var inventory = await dispatcher.SendAsync(new GetInventoryByIdQuery(callerId, inventoryId), cancellationToken);
            return inventory is null ? Results.NotFound() : Results.Ok(ToDto(inventory, callerId));
        });

        // Creating an inventory takes its name, not its contents - items are created through the save
        // below, exactly as task items are through their task list. Anything sent here would have been
        // dropped without a word, leaving the caller holding an inventory that quietly lost what it was
        // told to keep, so it is refused instead.
        inventories.MapPost("/", async (
            SaveInventoryRequest request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            if (request.Items.Count > 0)
            {
                throw new InvalidRequestException(
                    "An inventory is created with a name and filled afterwards - send its items to PUT /api/inventories/{id} instead.");
            }

            var id = await dispatcher.SendAsync(new CreateInventoryCommand(
                    GetUserId(user), request.Name, request.IsPrivate, ToDomainPayload(request.EncryptedContent),
                    request.Description), cancellationToken);
            return Results.Created($"/api/inventories/{id}", id);
        });

        inventories.MapPut("/{inventoryId:guid}", async (
            Guid inventoryId, SaveInventoryRequest request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var outcome = await dispatcher.SendAsync(
                new UpdateInventoryCommand(
                    GetUserId(user), inventoryId, request.Name, ToDomainItems(request.Items),
                    request.IsPrivate, ToDomainPayload(request.EncryptedContent), request.Description),
                cancellationToken);
            return ToApiResult(outcome);
        });

        inventories.MapDelete("/{inventoryId:guid}", async (
            Guid inventoryId, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var deleted = await dispatcher.SendAsync(new DeleteInventoryCommand(GetUserId(user), inventoryId), cancellationToken);
            return deleted ? Results.NoContent() : Results.NotFound();
        });

        // How this inventory's restock list is built, and when it comes round. On the inventory rather
        // than on the task list: it is the inventory's choice about a list it owns, and the list may not
        // exist yet - see RestockListSettings.
        inventories.MapGet("/{inventoryId:guid}/restock-list/settings", async (
            Guid inventoryId, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var settings = await dispatcher.SendAsync(
                new GetRestockListSettingsQuery(GetUserId(user), inventoryId), cancellationToken);
            return settings is null
                ? Results.NotFound()
                : Results.Ok(new RestockListSettingsDto(
                    settings.OnlyLinkedWithDueDate, settings.RefreshTimeOfDay,
                    settings.IsEnabled, settings.RemindDaily, settings.ListPriority.ToString()));
        });

        inventories.MapPut("/{inventoryId:guid}/restock-list/settings", async (
            Guid inventoryId, RestockListSettingsDto request, ClaimsPrincipal user, IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var outcome = await dispatcher.SendAsync(
                new SaveRestockListSettingsCommand(
                    GetUserId(user), inventoryId,
                    new RestockListSettings(
                        request.OnlyLinkedWithDueDate, request.RefreshTimeOfDay,
                        request.IsEnabled, request.RemindDaily,
                        RequestEnum.Parse<ItemPriority>(request.ListPriority, "listPriority"))),
                cancellationToken);
            return Results.Ok(new RestockRefreshResultDto(outcome.Added, outcome.Removed));
        });

        // The Refresh button: rebuild the list against the settings it already has. What somebody presses
        // when the world changed rather than the settings.
        inventories.MapPost("/{inventoryId:guid}/restock-list/refresh", async (
            Guid inventoryId, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var outcome = await dispatcher.SendAsync(
                new RefreshRestockListCommand(GetUserId(user), inventoryId), cancellationToken);
            return Results.Ok(new RestockRefreshResultDto(outcome.Added, outcome.Removed));
        });

        inventories.MapPost("/{inventoryId:guid}/shares", async (
            Guid inventoryId, ShareInventoryRequest request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var outcome = await dispatcher.SendAsync(
                new ShareInventoryCommand(
                    GetUserId(user), inventoryId, request.RecipientUserId,
                    RequestEnum.Parse<ShareAccessLevel>(request.AccessLevel, "accessLevel")),
                cancellationToken);
            return outcome is null ? Results.NotFound() : Results.Ok(new ShareResultDto(outcome.ShareId, outcome.AlreadyShared, outcome.AccessLevelRaised));
        }).RequireAuthorization(PermissionPolicies.Sharing);

        inventories.MapPost("/shares/{shareId:guid}/accept", async (
            Guid shareId, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var accepted = await dispatcher.SendAsync(new AcceptInventoryShareCommand(GetUserId(user), shareId), cancellationToken);
            return accepted ? Results.NoContent() : Results.NotFound();
        }).RequireAuthorization(PermissionPolicies.Sharing);

        // Lets Chat.razor show an accurate "Accept" vs. "already accepted" state for an inventory-share
        // message even after a page reload, instead of only remembering what was clicked this session.
        inventories.MapGet("/shares/{shareId:guid}/status", async (
            Guid shareId, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var isAccepted = await dispatcher.SendAsync(new GetInventoryShareStatusQuery(GetUserId(user), shareId), cancellationToken);
            return isAccepted is null ? Results.NotFound() : Results.Ok(isAccepted);
        }).RequireAuthorization(PermissionPolicies.Sharing);

        inventories.MapGet("/{inventoryId:guid}/items", async (
            Guid inventoryId, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(new GetInventoryItemsQuery(GetUserId(user), inventoryId), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result.Select(ToDto));
        });

        // Items have no routes of their own: they are created, changed, and removed through the inventory
        // save above, exactly as task items are through their task list.
        inventories.MapPost("/{inventoryId:guid}/lock", async (
            Guid inventoryId, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var outcome = await dispatcher.SendAsync(new AcquireInventoryLockCommand(GetUserId(user), inventoryId), cancellationToken);
            return outcome.Kind switch
            {
                EditOutcomeKind.Success => Results.NoContent(),
                EditOutcomeKind.Locked => Results.Json(new LockConflictDto(outcome.LockedByUserName!), statusCode: StatusCodes.Status409Conflict),
                _ => Results.NotFound()
            };
        });

        inventories.MapDelete("/{inventoryId:guid}/lock", async (
            Guid inventoryId, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var released = await dispatcher.SendAsync(new ReleaseInventoryLockCommand(GetUserId(user), inventoryId), cancellationToken);
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

    private static InventoryDto ToDto(Inventory inventory, Guid callerId)
        => new(
            inventory.Id, inventory.Name, inventory.CreatedAtUtc, inventory.UpdatedAtUtc,
            inventory.IsShared, inventory.SharedByUserName, inventory.AccessLevel.ToString(),
            // Only someone ELSE's live lock is worth telling the caller about: their own lock never
            // blocks them, and an expired one is free for the taking - see IsLockedByAnotherUser.
            inventory.IsLockedByAnotherUser(callerId, DateTimeOffset.UtcNow) ? inventory.LockedByUserName : null,
            inventory.IsShared ? inventory.UserId : null,
            inventory.IsPrivate,
            ToDto(inventory.EncryptedContent),
            inventory.IsSharedWithOthers,
            inventory.Description);


    /// <summary>Both halves travel together or not at all, so a request carrying only one is treated as carrying neither.</summary>
    private static EncryptedPayload? ToDomainPayload(EncryptedContentDto? encryptedContent)
        => encryptedContent is null ? null : new EncryptedPayload(encryptedContent.Ciphertext, encryptedContent.Nonce);

    private static EncryptedContentDto? ToDto(EncryptedPayload? encryptedContent)
        => encryptedContent is null ? null : new EncryptedContentDto(encryptedContent.Ciphertext, encryptedContent.Nonce);

    internal static IReadOnlyList<InventoryItemInput> ToDomainItems(IReadOnlyList<InventoryItemRequest> items)
        => items
            .Select(item => new InventoryItemInput(
                item.Id, item.Name, item.ProductType, item.Category, item.Quantity, item.MinimumQuantity,
                UnitOf(item), item.ExpiryDate,
                RequestEnum.Parse<NotificationChannel>(item.ExpiryNotificationChannel, "expiryNotificationChannel"),
                item.IsCheckedRegularly))
            .ToList();

    /// <summary>
    /// An item that says nothing about its unit is counted in pieces - the rule the whole feature is
    /// written to (see InventoryUnit), and what every row already on a shelf was given when the column
    /// was added. Refusing the save instead turned a client built before units existed - a cached copy
    /// of the app, say - into one that could no longer save an inventory at all, with a message about a
    /// field its version has never heard of. A unit that is named but not recognised is still refused:
    /// that is a typo, not a silence.
    /// </summary>
    internal static InventoryUnit UnitOf(InventoryItemRequest item)
        => string.IsNullOrWhiteSpace(item.Unit)
            ? InventoryUnit.Piece
            : RequestEnum.Parse<InventoryUnit>(item.Unit, "unit");

    private static InventoryItemDto ToDto(InventoryItem item)
        => new(
            item.Id, item.Name, item.ProductType, item.Category, item.Quantity, item.MinimumQuantity,
            item.Unit.ToString(), item.ExpiryDate, item.ExpiryNotificationChannel.ToString(), item.IsBelowMinimum,
            item.PendingRestockTaskItemId is not null, item.CreatedAtUtc, item.UpdatedAtUtc,
            item.IsCheckedRegularly);

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
