using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Orbit.Api.Sync;
using Orbit.Contracts;
using Orbit.Contracts.Calendar;
using Orbit.Contracts.Places;
using Orbit.Core.Abstractions;
using Orbit.Core.Calendar;
using Orbit.Core.Places;
using Orbit.Core.Places.CreatePlace;
using Orbit.Core.Places.DeletePlace;
using Orbit.Core.Places.DuplicatePlace;
using Orbit.Core.Places.GetPlaceById;
using Orbit.Core.Places.GetPlaces;
using Orbit.Core.Places.UpdatePlace;
using Orbit.Core.Sync;

namespace Orbit.Api.Places;

/// <summary>
/// Somewhere on the map worth keeping - see Orbit.Core.Places.Place. Shaped after NoteEndpoints, which
/// is the simplest module here and the one a place most resembles: one owner, no sharing yet, and a
/// delta feed so a client can hold its own copy.
/// </summary>
public static class PlaceEndpoints
{
    public static void MapPlaceEndpoints(this WebApplication app)
    {
        // Every place belongs to exactly one user, so the whole group requires an authenticated caller.
        var places = app.MapGroup("/api/places").RequireAuthorization();

        places.MapGet("/", async (ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(new GetPlacesQuery(GetUserId(user)), cancellationToken);
            return Results.Ok(result.Select(ToDto));
        });

        // What a client needs to catch up after being away: what changed, and what is gone. Its own
        // endpoint rather than a parameter on the list above, so the full-list shape stays as it is -
        // the same split the notes make.
        places.MapGet("/changes", async (
            DateTimeOffset since, ClaimsPrincipal user, IDispatcher dispatcher,
            ISyncTombstoneRepository tombstones, CancellationToken cancellationToken) =>
        {
            var userId = GetUserId(user);
            var cursor = ChangeFeed.StartCursor();
            var changed = await dispatcher.SendAsync(new GetPlacesQuery(userId, since), cancellationToken);

            return Results.Ok(await ChangeFeed.BuildAsync(
                changed.Select(ToDto).ToList(), cursor, userId, SyncEntityType.Place, since, tombstones,
                cancellationToken));
        });

        places.MapGet("/{id:guid}", async (
            Guid id, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var place = await dispatcher.SendAsync(new GetPlaceByIdQuery(GetUserId(user), id), cancellationToken);
            return place is null ? Results.NotFound() : Results.Ok(ToDto(place));
        });

        places.MapPost("/", async (
            SavePlaceRequest request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var id = await dispatcher.SendAsync(
                new CreatePlaceCommand(
                    GetUserId(user), request.Name, request.Description, ToDomain(request.Where),
                    request.Colour, RequestEnum.Parse<ItemPriority>(request.Priority, "priority"),
                    request.TaskListIds),
                cancellationToken);
            return Results.Created($"/api/places/{id}", id);
        });

        places.MapPut("/{id:guid}", async (
            Guid id, SavePlaceRequest request, ClaimsPrincipal user, IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var saved = await dispatcher.SendAsync(
                new UpdatePlaceCommand(
                    GetUserId(user), id, request.Name, request.Description, ToDomain(request.Where),
                    request.Colour, RequestEnum.Parse<ItemPriority>(request.Priority, "priority"),
                    request.TaskListIds),
                cancellationToken);
            return saved ? Results.NoContent() : Results.NotFound();
        });

        // A second place in the same spot - see DuplicatePlaceCommand. The body is optional, and a
        // caller that sends none keeps the original's name.
        places.MapPost("/{id:guid}/duplicate", async (
            Guid id, DuplicateRequest? request, ClaimsPrincipal user, IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var copyId = await dispatcher.SendAsync(
                new DuplicatePlaceCommand(GetUserId(user), id, request?.Name), cancellationToken);
            return copyId is { } newId ? Results.Created($"/api/places/{newId}", newId) : Results.NotFound();
        });

        places.MapDelete("/{id:guid}", async (
            Guid id, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var deleted = await dispatcher.SendAsync(new DeletePlaceCommand(GetUserId(user), id), cancellationToken);
            return deleted ? Results.NoContent() : Results.NotFound();
        });
    }

    private static PlaceDto ToDto(Place place)
        => new(
            place.Id, place.Name, place.Description,
            new EventLocationDto(place.Where.Address, place.Where.Latitude, place.Where.Longitude),
            place.Colour, place.Priority.ToString(), place.TaskListIds,
            place.CreatedAtUtc, place.UpdatedAtUtc);

    private static EventLocation ToDomain(EventLocationDto where)
        => new(where.Address, where.Latitude, where.Longitude);

    /// <inheritdoc cref="Orbit.Api.Notes.NoteEndpoints"/>
    private static Guid GetUserId(ClaimsPrincipal user)
    {
        var subject = user.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? throw new InvalidOperationException("Authenticated request is missing a 'sub' claim.");
        return Guid.Parse(subject);
    }
}
