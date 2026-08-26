using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Orbit.Contracts.Sharing;
using Orbit.Contracts;
using Orbit.Contracts.Tasks;
using Orbit.Core.Abstractions;
using Orbit.Core.Notifications;
using Orbit.Core.Tasks;
using Orbit.Core.Tasks.AcceptTaskListShare;
using Orbit.Core.Tasks.AcquireTaskListLock;
using Orbit.Core.Tasks.CreateTaskList;
using Orbit.Core.Tasks.DeleteTaskList;
using Orbit.Core.Tasks.GetTaskListById;
using Orbit.Core.Tasks.GetTaskListShareStatus;
using Orbit.Core.Tasks.GetTaskLists;
using Orbit.Core.Tasks.MoveTaskItem;
using Orbit.Core.Tasks.ReleaseTaskListLock;
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
                    ToDomainPayload(request.EncryptedContent), RequestEnum.Parse<TaskListPriority>(request.Priority, "priority")),
                cancellationToken);
            return Results.Created($"/api/tasks/{id}", id);
        });

        tasks.MapPut("/{id:guid}", async (
            Guid id, UpdateTaskRequest request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var outcome = await dispatcher.SendAsync(
                new UpdateTaskListCommand(
                    GetUserId(user), id, request.Title, ToDomainItems(request.Items), request.IsGroup, request.IsPrivate,
                    ToDomainPayload(request.EncryptedContent), RequestEnum.Parse<TaskListPriority>(request.Priority, "priority")),
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
        });

        // Resolves a share offered to the caller into a copy in their own task lists - see AcceptTaskListShareCommand.
        tasks.MapPost("/shares/{shareId:guid}/accept", async (
            Guid shareId, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var accepted = await dispatcher.SendAsync(new AcceptTaskListShareCommand(GetUserId(user), shareId), cancellationToken);
            return accepted ? Results.NoContent() : Results.NotFound();
        });

        // Lets Chat.razor show an accurate "Accept" vs. "already accepted" state for a task-list-share
        // message even after a page reload, instead of only remembering what was clicked this session.
        tasks.MapGet("/shares/{shareId:guid}/status", async (
            Guid shareId, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var isAccepted = await dispatcher.SendAsync(new GetTaskListShareStatusQuery(GetUserId(user), shareId), cancellationToken);
            return isAccepted is null ? Results.NotFound() : Results.Ok(isAccepted);
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

        if (item.Id is not { } existingId)
        {
            return TaskItem.Create(
                item.Description, item.DueDateUtc, item.IsCompleted, item.LinkedTaskListId,
                overdueChannel, item.RemindDaily, dailyChannel, item.DailyReminderTimeOfDay);
        }

        // Same override Create applies: a linked entry's completion follows the list it links to, so a
        // value sent for it is ignored rather than briefly believed - see LinkedTaskCompletionResolver.
        return TaskItem.FromPersistence(
            existingId, item.Description, item.DueDateUtc,
            item.LinkedTaskListId is null && item.IsCompleted, item.LinkedTaskListId,
            overdueChannel, item.RemindDaily, dailyChannel, item.DailyReminderTimeOfDay);
    }


    /// <summary>Both halves travel together or not at all, so a request carrying only one is treated as carrying neither.</summary>
    private static EncryptedPayload? ToDomainPayload(EncryptedContentDto? encryptedContent)
        => encryptedContent is null ? null : new EncryptedPayload(encryptedContent.Ciphertext, encryptedContent.Nonce);

    private static EncryptedContentDto? ToDto(EncryptedPayload? encryptedContent)
        => encryptedContent is null ? null : new EncryptedContentDto(encryptedContent.Ciphertext, encryptedContent.Nonce);

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
                    item.LinkedTaskListId,
                    item.OverdueNotificationChannel.ToString(),
                    item.RemindDaily,
                    item.DailyReminderNotificationChannel.ToString(),
                    item.DailyReminderTimeOfDay))
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
            taskList.IsPinned);

    /// <summary>Maps an EditOutcome onto the corresponding HTTP response - shared by the update and lock-acquire endpoints above.</summary>
    private static IResult ToApiResult(EditOutcome outcome) => outcome.Kind switch
    {
        EditOutcomeKind.Success => Results.NoContent(),
        EditOutcomeKind.Locked => Results.Json(new LockConflictDto(outcome.LockedByUserName!), statusCode: StatusCodes.Status409Conflict),
        _ => Results.NotFound()
    };
}
