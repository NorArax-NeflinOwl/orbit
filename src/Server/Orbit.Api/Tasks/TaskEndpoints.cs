using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Orbit.Contracts.Tasks;
using Orbit.Core.Abstractions;
using Orbit.Core.Notifications;
using Orbit.Core.Tasks;
using Orbit.Core.Tasks.AcceptTaskListShare;
using Orbit.Core.Tasks.CreateTaskList;
using Orbit.Core.Tasks.DeleteTaskList;
using Orbit.Core.Tasks.GetTaskListById;
using Orbit.Core.Tasks.GetTaskListShareStatus;
using Orbit.Core.Tasks.GetTaskLists;
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
                new CreateTaskListCommand(GetUserId(user), request.Title, ToDomainItems(request.Items)), cancellationToken);
            return Results.Created($"/api/tasks/{id}", id);
        });

        tasks.MapPut("/{id:guid}", async (
            Guid id, UpdateTaskRequest request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var updated = await dispatcher.SendAsync(
                new UpdateTaskListCommand(GetUserId(user), id, request.Title, ToDomainItems(request.Items)), cancellationToken);
            return updated ? Results.NoContent() : Results.NotFound();
        });

        tasks.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var deleted = await dispatcher.SendAsync(new DeleteTaskListCommand(GetUserId(user), id), cancellationToken);
            return deleted ? Results.NoContent() : Results.NotFound();
        });

        // Offers a copy of an owned task list to another user - see ShareTaskListCommand. The client is
        // responsible for notifying the recipient (a chat message carrying the returned share id), since
        // only the browser holds the key material to encrypt that message - mirrors CalendarEndpoints'
        // equivalent share endpoint.
        tasks.MapPost("/{id:guid}/shares", async (
            Guid id, ShareTaskListRequest request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var shareId = await dispatcher.SendAsync(
                new ShareTaskListCommand(GetUserId(user), id, request.RecipientUserId, Enum.Parse<ShareAccessLevel>(request.AccessLevel, ignoreCase: true)),
                cancellationToken);
            return shareId is null ? Results.NotFound() : Results.Ok(shareId);
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

    private static IReadOnlyList<TaskItem> ToDomainItems(IReadOnlyList<TaskItemRequest> items)
        => items
            .Select(item => TaskItem.Create(
                item.Description,
                item.DueDateUtc,
                item.IsCompleted,
                item.LinkedTaskListId,
                Enum.Parse<NotificationChannel>(item.OverdueNotificationChannel, ignoreCase: true),
                item.RemindDaily,
                Enum.Parse<NotificationChannel>(item.DailyReminderNotificationChannel, ignoreCase: true),
                item.DailyReminderTimeOfDay))
            .ToList();

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
            taskList.CreatedAtUtc,
            taskList.UpdatedAtUtc,
            taskList.IsShared,
            taskList.SharedByUserName,
            taskList.AccessLevel.ToString());
}
