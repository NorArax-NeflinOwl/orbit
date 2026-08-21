using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Orbit.Contracts.Tasks;
using Orbit.Core.Abstractions;
using Orbit.Core.Notifications;
using Orbit.Core.Tasks;
using Orbit.Core.Tasks.CreateTaskList;
using Orbit.Core.Tasks.DeleteTaskList;
using Orbit.Core.Tasks.GetTaskListById;
using Orbit.Core.Tasks.GetTaskLists;
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
            taskList.UpdatedAtUtc);
}
