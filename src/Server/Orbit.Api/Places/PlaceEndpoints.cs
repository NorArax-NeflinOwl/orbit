using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Orbit.Api.Sync;
using Orbit.Contracts;
using Orbit.Contracts.Calendar;
using Orbit.Contracts.Places;
using Orbit.Core.Abstractions;
using Orbit.Core.Calendar;
using Orbit.Core.Places;
using Orbit.Core.Places.AcceptPlaceShare;
using Orbit.Core.Places.CreatePlace;
using Orbit.Core.Places.DeletePlace;
using Orbit.Core.Places.DuplicatePlace;
using Orbit.Core.Places.GetPlaceById;
using Orbit.Core.Places.GetPlaceShareStatus;
using Orbit.Core.Places.GetPlaces;
using Orbit.Core.Places.SharePlace;
using Orbit.Core.Places.UpdatePlace;
using Orbit.Contracts.Sharing;
using Orbit.Core.Sync;

namespace Orbit.Api.Places;

/// <summary>
/// Somewhere on the map worth keeping - see Orbit.Core.Places.Place. Shaped after NoteEndpoints, which
/// is the simplest module here and the one a place most resembles: one owner, a delta feed so a client
/// can hold its own copy, and a share that grants access to that one row rather than making a copy.
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
                    request.TaskListIds, request.IsPrivate, ToDomain(request.EncryptedContent),
                    request.SourceTaskItemId),
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
                    request.TaskListIds, request.IsPrivate, ToDomain(request.EncryptedContent),
                    request.SourceTaskItemId),
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

        // Hands a place to somebody - see SharePlaceCommand. Announcing it is the client's job, the same
        // way it is for a note: a chat message carrying the share id returned here is what the recipient
        // presses Accept on.
        places.MapPost("/{id:guid}/shares", async (
            Guid id, SharePlaceRequest request, ClaimsPrincipal user, IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var outcome = await dispatcher.SendAsync(
                new SharePlaceCommand(
                    GetUserId(user), id, request.RecipientUserId,
                    RequestEnum.Parse<ShareAccessLevel>(request.AccessLevel, "accessLevel")),
                cancellationToken);
            return outcome is null
                ? Results.NotFound()
                : Results.Ok(new ShareResultDto(outcome.ShareId, outcome.AlreadyShared, outcome.AccessLevelRaised));
        });

        // Takes up an offer made to the caller - see AcceptPlaceShareCommand. From then on the place is
        // on their own map, and it is the same row the person who keeps it is looking at.
        places.MapPost("/shares/{shareId:guid}/accept", async (
            Guid shareId, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var accepted = await dispatcher.SendAsync(
                new AcceptPlaceShareCommand(GetUserId(user), shareId), cancellationToken);
            return accepted ? Results.NoContent() : Results.NotFound();
        });

        // What lets a conversation draw "Accept" against "already accepted" on an invitation it is
        // showing - see Chat.razor, and NoteEndpoints, which carries the same three.
        places.MapGet("/shares/{shareId:guid}/status", async (
            Guid shareId, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var isAccepted = await dispatcher.SendAsync(
                new GetPlaceShareStatusQuery(GetUserId(user), shareId), cancellationToken);
            return isAccepted is null ? Results.NotFound() : Results.Ok(isAccepted.Value);
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
            place.CreatedAtUtc, place.UpdatedAtUtc,
            // The owner's id only where this reader is not the owner, which is how "mine" reads on the
            // wire - the same shape NoteEndpoints sends.
            place.IsShared, place.SharedByUserName, place.AccessLevel.ToString(),
            place.IsShared ? place.UserId : null, place.IsSharedWithOthers,
            // The sealed half travels exactly as it is stored: the server has no key and never did.
            place.IsPrivate,
            place.EncryptedContent is { } sealedContent
                ? new EncryptedContentDto(sealedContent.Ciphertext, sealedContent.Nonce)
                : null);

    private static EventLocation ToDomain(EventLocationDto where)
        => new(where.Address, where.Latitude, where.Longitude);

    private static EncryptedPayload? ToDomain(EncryptedContentDto? sealedContent)
        => sealedContent is null ? null : new EncryptedPayload(sealedContent.Ciphertext, sealedContent.Nonce);

    /// <inheritdoc cref="Orbit.Api.Notes.NoteEndpoints"/>
    private static Guid GetUserId(ClaimsPrincipal user)
    {
        var subject = user.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? throw new InvalidOperationException("Authenticated request is missing a 'sub' claim.");
        return Guid.Parse(subject);
    }
}
