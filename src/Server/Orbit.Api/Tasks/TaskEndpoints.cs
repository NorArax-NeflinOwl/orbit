using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Orbit.Contracts.Sharing;
using Orbit.Api.Permissions;
using Orbit.Contracts;
using Orbit.Contracts.Tasks;
using Orbit.Core.Abstractions;
using Orbit.Core.Notifications;
using Orbit.Api.Sync;
using Orbit.Core.Sync;
using Orbit.Core.Tasks;
using Orbit.Core.Tasks.AcceptTaskListShare;
using Orbit.Core.Tasks.AcquireTaskListLock;
using Orbit.Core.Tasks.CreateTaskList;
using Orbit.Core.Tasks.DeleteTaskList;
using Orbit.Core.Tasks.GetTaskListById;
using Orbit.Core.Tasks.GetTaskListShareStatus;
using Orbit.Core.Tasks.GetTaskLists;
using Orbit.Core.Tasks.LinkCalendarEventToTaskList;
using Orbit.Core.Tasks.MoveTaskItem;
using Orbit.Core.Tasks.ReleaseTaskListLock;
using Orbit.Core.Tasks.LinkTaskListToWarehouse;
using Orbit.Core.Inventory.FinishRestocking;
using Orbit.Core.Inventory.ReconcileRestockList;
using Orbit.Core.Tasks.GetInventoryReferences;
using Orbit.Core.Tasks.ReconcileTaskListWithStock;
using Orbit.Core.Tasks.GenerateWarehouseFromTaskList;
using Orbit.Core.Tasks.GetTaskListStockCheck;
using Orbit.Core.Tasks.RaiseStockShortfalls;
using Orbit.Core.Tasks.SetTaskListPinned;
using Orbit.Core.Tasks.ShareTaskList;
using Orbit.Core.Tasks.UpdateTaskList;

namespace Orbit.Api.Tasks;

public static class TaskEndpoints
{
    public static void MapTaskEndpoints(this WebApplication app)
    {
        // Every task list belongs to exactly one user (see GetUserId below), so the whole group
        // requires a valid, authenticated caller.
        var tasks = app.MapGroup("/api/tasks").RequireAuthorization();

        tasks.MapGet("/", async (ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(new GetTaskListsQuery(GetUserId(user)), cancellationToken);
            return Results.Ok(result.Select(ToDto));
        });

        // What a client needs to catch up after being away - see ChangeFeedDto. Separate from GET /
        // so the existing full-list shape stays exactly as the web client expects it.
        tasks.MapGet("/changes", async (
            DateTimeOffset since, ClaimsPrincipal user, IDispatcher dispatcher,
            ISyncTombstoneRepository tombstones, CancellationToken cancellationToken) =>
        {
            var userId = GetUserId(user);
            var cursor = ChangeFeed.StartCursor();
            // The cursor goes to the database rather than being applied to everything it returned.
            var all = await dispatcher.SendAsync(new GetTaskListsQuery(userId, since), cancellationToken);
            var changed = all.Select(ToDto).ToList();

            return Results.Ok(await ChangeFeed.BuildAsync(
                changed, cursor, userId, SyncEntityType.TaskList, since, tombstones, cancellationToken));
        });

        tasks.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var taskList = await dispatcher.SendAsync(new GetTaskListByIdQuery(GetUserId(user), id), cancellationToken);
            return taskList is null ? Results.NotFound() : Results.Ok(ToDto(taskList));
        });

        tasks.MapPost("/", async (
            CreateTaskRequest request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var id = await dispatcher.SendAsync(
                new CreateTaskListCommand(
                    GetUserId(user), request.Title, ToDomainItems(request.Items), request.IsGroup, request.IsPrivate,
                    ToDomainPayload(request.EncryptedContent), RequestEnum.Parse<ItemPriority>(request.Priority, "priority"),
                    request.Description),
                cancellationToken);
            return Results.Created($"/api/tasks/{id}", id);
        });

        tasks.MapPut("/{id:guid}", async (
            Guid id, UpdateTaskRequest request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var outcome = await dispatcher.SendAsync(
                new UpdateTaskListCommand(
                    GetUserId(user), id, request.Title, ToDomainItems(request.Items), request.IsGroup, request.IsPrivate,
                    ToDomainPayload(request.EncryptedContent), RequestEnum.Parse<ItemPriority>(request.Priority, "priority"),
                    request.Description),
                cancellationToken);
            return ToApiResult(outcome);
        });

        tasks.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var deleted = await dispatcher.SendAsync(new DeleteTaskListCommand(GetUserId(user), id), cancellationToken);
            return deleted ? Results.NoContent() : Results.NotFound();
        });

        // Moves one item out of this list and into another of the caller's own lists - see
        // MoveTaskItemCommandHandler for why this needs its own endpoint rather than folding into the
        // whole-list PUT above (it touches two different TaskList aggregates at once).
        // Its own endpoint rather than part of the update: pinning is done from the list of lists,
        // where nothing has been loaded to edit - see TaskList.SetPinned.
        // Which warehouse this list's work is measured against. Its own endpoint for the same reason
        // pinning has one: it changes what the list is compared with, not what is on it.
        tasks.MapPut("/{id:guid}/warehouse", async (
            Guid id, LinkTaskListToWarehouseRequest request, ClaimsPrincipal user, IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var linked = await dispatcher.SendAsync(
                new LinkTaskListToWarehouseCommand(GetUserId(user), id, request.WarehouseId), cancellationToken);
            return linked ? Results.NoContent() : Results.NotFound();
        });

        // Builds the shelf this list's work needs - one entry per distinct thing it calls for - and
        // points the list at it, so the check below can be run straight away.
        tasks.MapPost("/{id:guid}/inventory", async (
            Guid id, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var warehouseId = await dispatcher.SendAsync(
                new GenerateWarehouseFromTaskListCommand(GetUserId(user), id), cancellationToken);
            return warehouseId is null ? Results.NotFound() : Results.Ok(warehouseId);
        });

        // Brings the list and the warehouse back into step both ways - the other half of the check
        // below, so the reader is neither left ticking by hand what the panel just told them is on the
        // shelf, nor left with a shelf holding things no list has heard of.
        tasks.MapPost("/{id:guid}/stock-check/reconciliation", async (
            Guid id, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var reconciliation = await dispatcher.SendAsync(
                new ReconcileTaskListWithStockCommand(GetUserId(user), id), cancellationToken);
            return Results.Ok(new StockReconciliationResultDto(reconciliation.CrossedOff, reconciliation.Added));
        });

        // "Everything on this list is done" - see FinishRestockingCommandHandler. Its own endpoint
        // rather than part of the save above, because it is a claim about the warehouse rather than an
        // edit to the list.
        tasks.MapPost("/{id:guid}/restocking/finished", async (
            Guid id, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var toppedUp = await dispatcher.SendAsync(new FinishRestockingCommand(GetUserId(user), id), cancellationToken);
            return Results.Ok(new FinishRestockingResultDto(toppedUp));
        });

        // Settles the finished errands on a restock list: each one fills its shelf item and then leaves
        // the list. Asked for when the checklist screen opens one, which is what clears errands ticked
        // off before this existed - see ReconcileRestockListCommand.
        tasks.MapPost("/{id:guid}/restocking/reconcile", async (
            Guid id, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var outcome = await dispatcher.SendAsync(
                new ReconcileRestockListCommand(GetUserId(user), id), cancellationToken);
            return Results.Ok(new RestockReconciliationResultDto(outcome.ToppedUp, outcome.Removed));
        });

        // What each inventory errand on this list is about: the shelf item, and any other list asking for
        // the same thing. Its own route rather than fields on the list, because neither belongs to the
        // list - see GetInventoryReferencesQuery.
        tasks.MapGet("/{id:guid}/inventory-references", async (
            Guid id, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var references = await dispatcher.SendAsync(
                new GetInventoryReferencesQuery(GetUserId(user), id), cancellationToken);
            return Results.Ok(references.Select(ToDto));
        });

        // Whether this list's work - and everything linked below it - can be done out of that warehouse.
        // 404 rather than an empty answer when no warehouse has been chosen: there is no question yet.
        tasks.MapGet("/{id:guid}/stock-check", async (
            Guid id, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var check = await dispatcher.SendAsync(new GetTaskListStockCheckQuery(GetUserId(user), id), cancellationToken);
            return check is null ? Results.NotFound() : Results.Ok(ToDto(check));
        });

        // Puts what is short onto the warehouse's standing restock list, where the daily reminder brings
        // it up - see InventoryTaskListCoordinator.
        tasks.MapPost("/{id:guid}/stock-check/shortfalls", async (
            Guid id, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var added = await dispatcher.SendAsync(new RaiseStockShortfallsCommand(GetUserId(user), id), cancellationToken);
            return Results.Ok(new RaiseStockShortfallsResultDto(added));
        });

        tasks.MapPut("/{id:guid}/pinned", async (
            Guid id, SetTaskListPinnedRequest request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var pinned = await dispatcher.SendAsync(
                new SetTaskListPinnedCommand(GetUserId(user), id, request.IsPinned), cancellationToken);
            return pinned ? Results.NoContent() : Results.NotFound();
        });

        tasks.MapPost("/{id:guid}/items/{itemId:guid}/move", async (
            Guid id, Guid itemId, MoveTaskItemRequest request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var outcome = await dispatcher.SendAsync(
                new MoveTaskItemCommand(GetUserId(user), id, itemId, request.TargetTaskListId), cancellationToken);
            return ToApiResult(outcome);
        });

        // Puts an existing event on this list as an entry pointing at it. Its own endpoint rather than a
        // whole-list save: a client that had to read the list, add a row and send it all back would
        // overwrite whatever changed in between - see the command.
        tasks.MapPost("/{id:guid}/items/calendar-event", async (
            Guid id, LinkCalendarEventRequest request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var outcome = await dispatcher.SendAsync(
                new LinkCalendarEventToTaskListCommand(GetUserId(user), id, request.CalendarEventId), cancellationToken);
            return ToApiResult(outcome);
        });

        // Mirrors NoteEndpoints' equivalent lock endpoints - see AcquireTaskListLockCommand's comment.
        tasks.MapPost("/{id:guid}/lock", async (Guid id, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var outcome = await dispatcher.SendAsync(new AcquireTaskListLockCommand(GetUserId(user), id), cancellationToken);
            return ToApiResult(outcome);
        });

        tasks.MapDelete("/{id:guid}/lock", async (Guid id, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            await dispatcher.SendAsync(new ReleaseTaskListLockCommand(GetUserId(user), id), cancellationToken);
            return Results.NoContent();
        });

        // Offers a copy of an owned task list to another user - see ShareTaskListCommand. The client is
        // responsible for notifying the recipient (a chat message carrying the returned share id), since
        // only the browser holds the key material to encrypt that message - mirrors CalendarEndpoints'
        // equivalent share endpoint.
        tasks.MapPost("/{id:guid}/shares", async (
            Guid id, ShareTaskListRequest request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var outcome = await dispatcher.SendAsync(
                new ShareTaskListCommand(GetUserId(user), id, request.RecipientUserId, RequestEnum.Parse<ShareAccessLevel>(request.AccessLevel, "accessLevel")),
                cancellationToken);
            return outcome is null ? Results.NotFound() : Results.Ok(new ShareResultDto(outcome.ShareId, outcome.AlreadyShared, outcome.AccessLevelRaised));
        }).RequireAuthorization(PermissionPolicies.Sharing);

        // Resolves a share offered to the caller into a copy in their own task lists - see AcceptTaskListShareCommand.
        tasks.MapPost("/shares/{shareId:guid}/accept", async (
            Guid shareId, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var accepted = await dispatcher.SendAsync(new AcceptTaskListShareCommand(GetUserId(user), shareId), cancellationToken);
            return accepted ? Results.NoContent() : Results.NotFound();
        }).RequireAuthorization(PermissionPolicies.Sharing);

        // Lets Chat.razor show an accurate "Accept" vs. "already accepted" state for a task-list-share
        // message even after a page reload, instead of only remembering what was clicked this session.
        tasks.MapGet("/shares/{shareId:guid}/status", async (
            Guid shareId, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var isAccepted = await dispatcher.SendAsync(new GetTaskListShareStatusQuery(GetUserId(user), shareId), cancellationToken);
            return isAccepted is null ? Results.NotFound() : Results.Ok(isAccepted);
        }).RequireAuthorization(PermissionPolicies.Sharing);
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

    /// <summary>
    /// An entry that came back with an id keeps it; one without is new and gets a fresh one. Rebuilding
    /// every entry from scratch is what let an inventory item's restock task, a daily reminder's
    /// once-a-day record, and an overdue notification all point at rows that no longer existed after the
    /// reader so much as ticked a box.
    /// </summary>
    private static IReadOnlyList<TaskItem> ToDomainItems(IReadOnlyList<TaskItemRequest> items)
        => items.Select(ToDomainItem).ToList();

    private static TaskItem ToDomainItem(TaskItemRequest item)
    {
        var overdueChannel = RequestEnum.Parse<NotificationChannel>(item.OverdueNotificationChannel, "overdueNotificationChannel");
        var dailyChannel = RequestEnum.Parse<NotificationChannel>(item.DailyReminderNotificationChannel, "dailyReminderNotificationChannel");
        var kind = RequestEnum.Parse<TaskItemKind>(item.Kind, "kind");

        if (item.Id is not { } existingId)
        {
            return TaskItem.Create(
                item.Description, item.DueDateUtc, item.IsCompleted, item.AllLinkedTaskListIds,
                overdueChannel, item.RemindDaily, dailyChannel, item.DailyReminderTimeOfDay,
                kind, item.Location, item.LinkedCalendarEventId, item.LinkedInventoryItemId, item.AllCategories);
        }

        // Same override Create applies: a linked entry's completion follows the list it links to, so a
        // value sent for it is ignored rather than briefly believed - see LinkedTaskCompletionResolver.
        return TaskItem.FromPersistence(
            existingId, item.Description, item.DueDateUtc,
            item.AllLinkedTaskListIds.Count == 0 && item.IsCompleted, item.AllLinkedTaskListIds,
            overdueChannel, item.RemindDaily, dailyChannel, item.DailyReminderTimeOfDay,
            kind, item.Location, item.LinkedCalendarEventId, item.LinkedInventoryItemId, item.AllCategories);
    }


    /// <summary>Both halves travel together or not at all, so a request carrying only one is treated as carrying neither.</summary>
    private static EncryptedPayload? ToDomainPayload(EncryptedContentDto? encryptedContent)
        => encryptedContent is null ? null : new EncryptedPayload(encryptedContent.Ciphertext, encryptedContent.Nonce);

    private static EncryptedContentDto? ToDto(EncryptedPayload? encryptedContent)
        => encryptedContent is null ? null : new EncryptedContentDto(encryptedContent.Ciphertext, encryptedContent.Nonce);

    private static InventoryReferenceDto ToDto(InventoryReference reference)
        => new(
            reference.TaskItemId, reference.InventoryItemId, reference.InventoryItemName, reference.WarehouseId,
            reference.WarehouseName,
            [.. reference.AlsoAskedForBy.Select(elsewhere => new InventoryReferenceElsewhereDto(
                elsewhere.TaskListId, elsewhere.TaskListTitle, elsewhere.TaskItemId))]);

    private static TaskListStockCheckDto ToDto(Orbit.Core.Tasks.StockCheck.TaskListStockCheck check)
        => new(check.IsAchievable,
            [.. check.Requirements.Select(requirement => new StockRequirementDto(
                requirement.Name, requirement.Required, requirement.Available, requirement.Missing))]);

    private static TaskDto ToDto(TaskList taskList)
        => new(
            taskList.Id,
            taskList.Title,
            taskList.Items
                .Select(item => new TaskItemDto(
                    item.Id,
                    item.Description,
                    item.DueDateUtc,
                    item.IsCompleted,
                    // The first one repeated on its own, for a client that only knows the old field.
                    // Written out rather than FirstOrDefault, which gives the all-zero Guid for an
                    // entry that links to nothing - a link to a list nobody has.
                    item.LinkedTaskListIds.Count > 0 ? item.LinkedTaskListIds[0] : null,
                    item.OverdueNotificationChannel.ToString(),
                    item.RemindDaily,
                    item.DailyReminderNotificationChannel.ToString(),
                    item.DailyReminderTimeOfDay,
                    item.Kind.ToString(),
                    item.Location,
                    item.LinkedCalendarEventId,
                    item.LinkedInventoryItemId,
                    item.LinkedTaskListIds,
                    item.Categories))
                .ToList(),
            taskList.IsCompleted,
            taskList.IsGroup,
            taskList.IsPrivate,
            ToDto(taskList.EncryptedContent),
            taskList.CreatedAtUtc,
            taskList.UpdatedAtUtc,
            taskList.IsShared,
            taskList.SharedByUserName,
            taskList.AccessLevel.ToString(),
            taskList.IsShared ? taskList.UserId : null,
            taskList.Priority.ToString(),
            taskList.Status.ToString(),
            taskList.IsPinned, taskList.IsSharedWithOthers, taskList.LinkedWarehouseId, taskList.Description);

    /// <summary>Maps an EditOutcome onto the corresponding HTTP response - shared by the update and lock-acquire endpoints above.</summary>
    private static IResult ToApiResult(EditOutcome outcome) => outcome.Kind switch
    {
        EditOutcomeKind.Success => Results.NoContent(),
        EditOutcomeKind.Locked => Results.Json(new LockConflictDto(outcome.LockedByUserName!), statusCode: StatusCodes.Status409Conflict),
        // 403 rather than 404: the caller can see this, so hiding it from them now would only confuse.
        EditOutcomeKind.ReadOnly => Results.Json(
            new RefusalDto("This was shared with you to read, not to change."), statusCode: StatusCodes.Status403Forbidden),
        _ => Results.NotFound()
    };
}
