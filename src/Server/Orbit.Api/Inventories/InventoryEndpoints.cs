using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Orbit.Api.Permissions;
using Orbit.Contracts;
using Orbit.Core.Inventories.DuplicateInventory;
using Orbit.Contracts.Folders;
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
using Orbit.Core.Inventories.GatherInventories;
using Orbit.Core.Inventories.GetShelfDemand;
using Orbit.Core.Inventories.GetInventoryById;
using Orbit.Core.Inventories.GetInventories;
using Orbit.Core.Inventories.GetInventoryShareStatus;
using Orbit.Core.Inventories.MoveInventoryToFolder;
using Orbit.Core.Inventories.ReleaseInventoryLock;
using Orbit.Core.Inventories.ShareInventory;
using Orbit.Core.Inventories.UpdateInventory;
using Orbit.Core.Notifications;
using Orbit.Core.Inventories.ArchiveInventory;

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

        // Creating an inventory takes its name and whatever is already on the shelf. It used to take the
        // name alone and refuse anything else, on the grounds that items sent here would be dropped
        // without a word - but /inventory/new is a name-it-and-fill-it screen whose form button is "Add
        // item", so naming an inventory, adding a row and pressing Save was a save that could never
        // succeed. The answer to "they would be dropped" is to stop dropping them: the rows go through
        // the same InventoryItemsSaver a save uses, positions and restock tasks included.
        inventories.MapPost("/", async (
            SaveInventoryRequest request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var id = await dispatcher.SendAsync(new CreateInventoryCommand(
                    GetUserId(user), request.Name, request.IsPrivate, ToDomainPayload(request.EncryptedContent),
                    request.Description, ToDomainItems(request.Items), request.FolderId), cancellationToken);
            return Results.Created($"/api/inventories/{id}", id);
        });

        inventories.MapPut("/{inventoryId:guid}", async (
            Guid inventoryId, SaveInventoryRequest request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var outcome = await dispatcher.SendAsync(
                new UpdateInventoryCommand(
                    GetUserId(user), inventoryId, request.Name, ToDomainItems(request.Items),
                    request.IsPrivate, ToDomainPayload(request.EncryptedContent), request.Description,
                    request.SplitEvenlyAcross),
                cancellationToken);
            return ToApiResult(outcome);
        });

        // A second storage holding the same things - see DuplicateInventoryCommand. The body is
        // optional, and a caller that sends none keeps the original's name.
        inventories.MapPost("/{inventoryId:guid}/duplicate", async (
            Guid inventoryId, DuplicateRequest? request, ClaimsPrincipal user, IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var copyId = await dispatcher.SendAsync(
                new DuplicateInventoryCommand(GetUserId(user), inventoryId, request?.Name), cancellationToken);
            return copyId is { } newId ? Results.Created($"/api/inventories/{newId}", newId) : Results.NotFound();
        });

        // Filing is its own endpoint rather than a field on the save, the way a note's is - see
        // MoveInventoryToFolderCommand. NotFound covers both refusals: not the caller's inventory, and
        // not the caller's folder.
        inventories.MapPut("/{inventoryId:guid}/folder", async (
            Guid inventoryId, MoveToFolderRequest request, ClaimsPrincipal user, IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var moved = await dispatcher.SendAsync(
                new MoveInventoryToFolderCommand(GetUserId(user), inventoryId, request.FolderId), cancellationToken);
            return moved ? Results.NoContent() : Results.NotFound();
        });

        // Putting one away and bringing it back - see ArchiveInventoryCommand. Its own endpoint beside
        // the filing above, and for the same reason: an update carries the whole shelf.
        inventories.MapPut("/{inventoryId:guid}/archived", async (
            Guid inventoryId, ArchiveRequest request, ClaimsPrincipal user, IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var archived = await dispatcher.SendAsync(
                new ArchiveInventoryCommand(GetUserId(user), inventoryId, request.IsArchived), cancellationToken);
            return archived ? Results.NoContent() : Results.NotFound();
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
                    settings.IsEnabled, settings.RemindDaily, settings.ListPriority.ToString(),
                    settings.OnlyCheckedRegularly, settings.ReminderChannel.ToString()));
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
                        RequestEnum.Parse<ItemPriority>(request.ListPriority, "listPriority"),
                        request.OnlyCheckedRegularly,
                        RequestEnum.Parse<NotificationChannel>(request.ReminderChannel, "reminderChannel"))),
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

        // Which shelves this one gathers - see GatherInventoriesCommand, and the folder route above,
        // which is the same shape of decision: it changes what the shelf is read alongside rather than
        // what is on it, so it travels on its own and not in the save.
        inventories.MapPut("/{inventoryId:guid}/gathers", async (
            Guid inventoryId, GatherInventoriesRequest request, ClaimsPrincipal user, IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var gathered = await dispatcher.SendAsync(
                new GatherInventoriesCommand(GetUserId(user), inventoryId, request.InventoryIds), cancellationToken);
            // NotFound covers a shelf that is not theirs and a membership that would close a ring - see
            // InventoryGroups.MayGatherAsync. The page only ever offers shelves it just listed, so a
            // refusal here means the reader raced somebody rather than that they need telling which rule.
            return gathered ? Results.NoContent() : Results.NotFound();
        });

        // Which of the caller's task entries ask for each row on this shelf - see ShelfDemand. Read by an
        // editor before it saves, so changing an amount several lists are asking for can warn rather than
        // pick one of them at random.
        inventories.MapGet("/{inventoryId:guid}/demand", async (
            Guid inventoryId, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(new GetShelfDemandQuery(GetUserId(user), inventoryId), cancellationToken);
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
            inventory.Description,
            // Never the owner's filing - see NoteEndpoints, which says the same about a shared note.
            inventory.IsShared ? null : inventory.FolderId,
            // The owner's too - see NoteEndpoints.ToDto, which says why a recipient is told nothing.
            !inventory.IsShared && inventory.IsArchived,
            // And the group's membership, which is the owner's arrangement of shelves of their own.
            // A recipient holds none of those, so a list of ids they could not open would only be a
            // group drawn empty - see GatherInventoriesCommandHandler on who may arrange one.
            inventory.IsShared ? [] : inventory.GathersInventoryIds);


    /// <summary>Both halves travel together or not at all, so a request carrying only one is treated as carrying neither.</summary>
    private static EncryptedPayload? ToDomainPayload(EncryptedContentDto? encryptedContent)
        => encryptedContent is null ? null : new EncryptedPayload(encryptedContent.Ciphertext, encryptedContent.Nonce);

    private static EncryptedContentDto? ToDto(EncryptedPayload? encryptedContent)
        => encryptedContent is null ? null : new EncryptedContentDto(encryptedContent.Ciphertext, encryptedContent.Nonce);

    internal static IReadOnlyList<InventoryItemInput> ToDomainItems(IReadOnlyList<InventoryItemRequest> items)
        => items
            .Select(item => new InventoryItemInput(
                item.Id, item.Name, item.ProductType, item.AllCategories, item.Quantity, item.MinimumQuantity,
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
            // The first of them in the old single field too, so a client that has not learned about
            // several still reads one - see InventoryItemDto.Category.
            item.Id, item.Name, item.ProductType, item.Categories.FirstOrDefault() ?? string.Empty, item.Quantity,
            item.MinimumQuantity,
            item.Unit.ToString(), item.ExpiryDate, item.ExpiryNotificationChannel.ToString(), item.IsBelowMinimum,
            item.PendingRestockTaskItemId is not null, item.CreatedAtUtc, item.UpdatedAtUtc,
            item.IsCheckedRegularly, item.Categories, item.Usage);

    private static ShelfClaimDto ToDto(ShelfClaim claim)
        => new(
            claim.ShelfItemId, claim.TaskListId, claim.TaskListName, claim.TaskItemId, claim.Description,
            claim.Quantity);

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
