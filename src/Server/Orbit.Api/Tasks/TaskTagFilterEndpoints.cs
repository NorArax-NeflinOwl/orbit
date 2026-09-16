using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Orbit.Contracts.Tasks;
using Orbit.Core.Abstractions;
using Orbit.Core.Tasks.TagFilters;

namespace Orbit.Api.Tasks;

/// <summary>
/// The filters an account makes for the dashboard's Tasks card - see Orbit.Core.Tasks.TagFilters.TaskTagFilter.
/// Made and taken away, never changed: a filter is its tags, so a different set of tags is a different
/// filter, and one more endpoint to keep in step would buy nothing a delete and a create do not.
/// </summary>
public static class TaskTagFilterEndpoints
{
    public static void MapTaskTagFilterEndpoints(this WebApplication app)
    {
        var filters = app.MapGroup("/api/task-filters").RequireAuthorization();

        filters.MapGet("/", async (ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
            Results.Ok((await dispatcher.SendAsync(new GetTaskTagFiltersQuery(GetUserId(user)), cancellationToken))
                .Select(ToDto)));

        filters.MapPost("/", async (
            CreateTaskTagFilterRequest request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var created = await dispatcher.SendAsync(
                new CreateTaskTagFilterCommand(GetUserId(user), request.Tags ?? [], request.MatchesAll), cancellationToken);
            return Results.Created($"/api/task-filters/{created.Id}", ToDto(created));
        });

        filters.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
            await dispatcher.SendAsync(new DeleteTaskTagFilterCommand(GetUserId(user), id), cancellationToken)
                ? Results.NoContent()
                : Results.NotFound());
    }

    private static TaskTagFilterDto ToDto(TaskTagFilter filter)
        => new(filter.Id, filter.Tags, filter.MatchesAll, filter.CreatedAtUtc);

    /// <summary>The group requires authorization, and Orbit.Api only ever issues tokens with this claim - see NotificationEndpoints.</summary>
    private static Guid GetUserId(ClaimsPrincipal user)
    {
        var subject = user.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? throw new InvalidOperationException("Authenticated request is missing a 'sub' claim.");
        return Guid.Parse(subject);
    }
}
