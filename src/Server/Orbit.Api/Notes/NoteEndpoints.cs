using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Orbit.Api.Permissions;
using Orbit.Contracts;
using Orbit.Contracts.Notes;
using Orbit.Contracts.Sharing;
using Orbit.Core.Abstractions;
using Orbit.Core.Notes;
using Orbit.Core.Notes.AcceptNoteShare;
using Orbit.Core.Notes.AcquireNoteLock;
using Orbit.Core.Notes.CreateNote;
using Orbit.Core.Notes.DeleteNote;
using Orbit.Core.Notes.GetNoteById;
using Orbit.Core.Notes.GetNoteShareStatus;
using Orbit.Core.Notes.GetNotes;
using Orbit.Core.Notes.ReleaseNoteLock;
using Orbit.Core.Notes.SetNotePinned;
using Orbit.Core.Notes.ShareNote;
using Orbit.Core.Notes.UpdateNote;

namespace Orbit.Api.Notes;

public static class NoteEndpoints
{
    public static void MapNoteEndpoints(this WebApplication app)
    {
        // Every note belongs to exactly one user (see GetUserId below), so the whole group requires a
        // valid, authenticated caller.
        var notes = app.MapGroup("/api/notes").RequireAuthorization();

        notes.MapGet("/", async (ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(new GetNotesQuery(GetUserId(user)), cancellationToken);
            return Results.Ok(result.Select(ToDto));
        });

        notes.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var note = await dispatcher.SendAsync(new GetNoteByIdQuery(GetUserId(user), id), cancellationToken);
            return note is null ? Results.NotFound() : Results.Ok(ToDto(note));
        });

        notes.MapPost("/", async (
            CreateNoteRequest request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var id = await dispatcher.SendAsync(
                new CreateNoteCommand(GetUserId(user), request.Title, ToDomainContent(request.Content), request.IsPrivate, ToDomainPayload(request.EncryptedContent)), cancellationToken);
            return Results.Created($"/api/notes/{id}", id);
        });

        notes.MapPut("/{id:guid}", async (
            Guid id, UpdateNoteRequest request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var outcome = await dispatcher.SendAsync(
                new UpdateNoteCommand(GetUserId(user), id, request.Title, ToDomainContent(request.Content), request.IsPrivate, ToDomainPayload(request.EncryptedContent)), cancellationToken);
            return ToApiResult(outcome);
        });

        // Separate from the update above because pinning only moves a card on a page: it needs no body
        // to send back, takes no edit lock, and works from the list page where nothing has been loaded
        // to edit - see Note.SetPinned.
        notes.MapPut("/{id:guid}/pinned", async (
            Guid id, SetNotePinnedRequest request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var pinned = await dispatcher.SendAsync(
                new SetNotePinnedCommand(GetUserId(user), id, request.IsPinned), cancellationToken);
            return pinned ? Results.NoContent() : Results.NotFound();
        });

        notes.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var deleted = await dispatcher.SendAsync(new DeleteNoteCommand(GetUserId(user), id), cancellationToken);
            return deleted ? Results.NoContent() : Results.NotFound();
        });

        // Acquires (or refreshes) the edit lock on a note the caller has CanEdit access to - see
        // AcquireNoteLockCommand. NoteEditor.razor calls this once on opening an editable note, then
        // again on a heartbeat while the editor stays open.
        notes.MapPost("/{id:guid}/lock", async (Guid id, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var outcome = await dispatcher.SendAsync(new AcquireNoteLockCommand(GetUserId(user), id), cancellationToken);
            return ToApiResult(outcome);
        });

        // Releases the caller's own edit lock, if they hold one - a no-op otherwise. Always 204, since
        // there's nothing meaningful to distinguish from the caller's point of view (see
        // ReleaseNoteLockCommand).
        notes.MapDelete("/{id:guid}/lock", async (Guid id, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            await dispatcher.SendAsync(new ReleaseNoteLockCommand(GetUserId(user), id), cancellationToken);
            return Results.NoContent();
        });

        // Offers a copy of an owned note to another user - see ShareNoteCommand. The client is
        // responsible for notifying the recipient (a chat message carrying the returned share id),
        // since only the browser holds the key material to encrypt that message - mirrors
        // CalendarEndpoints' equivalent share endpoint.
        notes.MapPost("/{id:guid}/shares", async (
            Guid id, ShareNoteRequest request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var outcome = await dispatcher.SendAsync(
                new ShareNoteCommand(GetUserId(user), id, request.RecipientUserId, RequestEnum.Parse<ShareAccessLevel>(request.AccessLevel, "accessLevel")),
                cancellationToken);
            return outcome is null ? Results.NotFound() : Results.Ok(new ShareResultDto(outcome.ShareId, outcome.AlreadyShared, outcome.AccessLevelRaised));
        }).RequireAuthorization(PermissionPolicies.Sharing);

        // Resolves a share offered to the caller into a copy in their own notes - see AcceptNoteShareCommand.
        notes.MapPost("/shares/{shareId:guid}/accept", async (
            Guid shareId, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var accepted = await dispatcher.SendAsync(new AcceptNoteShareCommand(GetUserId(user), shareId), cancellationToken);
            return accepted ? Results.NoContent() : Results.NotFound();
        }).RequireAuthorization(PermissionPolicies.Sharing);

        // Lets Chat.razor show an accurate "Accept" vs. "already accepted" state for a note-share message
        // even after a page reload, instead of only remembering what was clicked this session.
        notes.MapGet("/shares/{shareId:guid}/status", async (
            Guid shareId, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var isAccepted = await dispatcher.SendAsync(new GetNoteShareStatusQuery(GetUserId(user), shareId), cancellationToken);
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

    private static IReadOnlyList<NoteContentLine> ToDomainContent(IReadOnlyList<NoteContentLineDto> content)
        => content.Select(line => new NoteContentLine(line.Text, line.IsChecklistItem, line.IsChecked)).ToList();

    private static NoteContentLineDto ToDto(NoteContentLine line)
        => new(line.Text, line.IsChecklistItem, line.IsChecked);


    /// <summary>Both halves travel together or not at all, so a request carrying only one is treated as carrying neither.</summary>
    private static EncryptedPayload? ToDomainPayload(EncryptedContentDto? encryptedContent)
        => encryptedContent is null ? null : new EncryptedPayload(encryptedContent.Ciphertext, encryptedContent.Nonce);

    private static EncryptedContentDto? ToDto(EncryptedPayload? encryptedContent)
        => encryptedContent is null ? null : new EncryptedContentDto(encryptedContent.Ciphertext, encryptedContent.Nonce);

    private static NoteDto ToDto(Note note)
        => new(
            note.Id, note.Title, note.Content.Select(ToDto).ToList(), note.IsPrivate, ToDto(note.EncryptedContent),
            note.CreatedAtUtc, note.UpdatedAtUtc,
            note.IsShared, note.SharedByUserName, note.AccessLevel.ToString(), note.IsShared ? note.UserId : null,
            note.IsPinned);

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
