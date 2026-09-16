using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Orbit.Contracts.Notes;
using Orbit.Core.Abstractions;
using Orbit.Core.Notes;
using Orbit.Core.Notes.AddNotePicture;
using Orbit.Core.Notes.DeleteNotePicture;
using Orbit.Core.Notes.GetNotePicture;

namespace Orbit.Api.Notes.Pictures;

/// <summary>
/// A note's pictures - see Orbit.Core.Notes.NotePicture. The bytes travel as the request body itself
/// rather than as a form: there is one thing to send and its Content-Type header already says what it
/// is. A sealed picture says so in a header of its own, since its body is ciphertext and its type is
/// sealed with the line that names it.
/// </summary>
public static class NotePictureEndpoints
{
    /// <summary>Set to "true" by a client that sealed the bytes before sending them - see PrivateContentSealer in Orbit.Web.</summary>
    public const string SealedHeader = "X-Orbit-Picture-Sealed";

    public static void MapNotePictureEndpoints(this WebApplication app)
    {
        var pictures = app.MapGroup("/api/notes/{id:guid}/pictures").RequireAuthorization();

        pictures.MapPost("/", async (
            Guid id, HttpRequest request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var isSealed = string.Equals(request.Headers[SealedHeader], "true", StringComparison.OrdinalIgnoreCase);
            var outcome = await dispatcher.SendAsync(
                new AddNotePictureCommand(
                    GetUserId(user), id, request.Body, request.ContentLength ?? 0,
                    isSealed ? null : request.ContentType, isSealed),
                cancellationToken);

            return outcome.Kind switch
            {
                AddNotePictureOutcomeKind.Added => Results.Created(
                    $"/api/notes/{id}/pictures/{outcome.Picture!.Id}", new NotePictureDto(outcome.Picture.Id, outcome.Picture.SizeBytes)),
                AddNotePictureOutcomeKind.TooLarge => Results.StatusCode(StatusCodes.Status413PayloadTooLarge),
                AddNotePictureOutcomeKind.MustBeSealed => Results.BadRequest("A private note's picture has to arrive sealed, and a public note's in the clear."),
                // NotFound and ReadOnly alike: which of the two it was is not the caller's to learn.
                _ => Results.NotFound()
            };
        })
        // One picture is one request, and Kestrel's default already bounds it at this size; said here so
        // the number is beside the endpoint it applies to rather than in a server default nobody reads.
        .WithMetadata(new RequestSizeLimitAttribute(NotePictureLimits.MaximumBytesPerPicture));

        pictures.MapGet("/{pictureId:guid}", async (
            Guid id, Guid pictureId, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var content = await dispatcher.SendAsync(new GetNotePictureQuery(GetUserId(user), id, pictureId), cancellationToken);
            return content is null ? Results.NotFound() : PictureResult(content);
        });

        pictures.MapDelete("/{pictureId:guid}", async (
            Guid id, Guid pictureId, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var deleted = await dispatcher.SendAsync(new DeleteNotePictureCommand(GetUserId(user), id, pictureId), cancellationToken);
            return deleted ? Results.NoContent() : Results.NotFound();
        });
    }

    /// <summary>
    /// The bytes as they are. A sealed picture goes out as application/octet-stream - it is ciphertext,
    /// and what it is inside is the line's to say once the browser has opened it. Never cached by
    /// anything between: the read is an access check, and a shared proxy must not answer the next
    /// reader with the last one's picture.
    /// </summary>
    public static IResult PictureResult(NotePictureContent content)
        => Results.Stream(content.Content, content.Picture.ContentType ?? "application/octet-stream");

    private static Guid GetUserId(ClaimsPrincipal user)
    {
        var subject = user.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? throw new InvalidOperationException("Authenticated request is missing a 'sub' claim.");
        return Guid.Parse(subject);
    }
}
