using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Orbit.Contracts.Notes;
using Orbit.Contracts.Sharing;
using Orbit.Core.Abstractions;
using Orbit.Core.Notes;
using Orbit.Core.Notes.AcceptNoteShare;
using Orbit.Core.Notes.CreateNote;
using Orbit.Core.Notes.DeleteNote;
using Orbit.Core.Notes.GetNoteById;
using Orbit.Core.Notes.GetNoteShareStatus;
using Orbit.Core.Notes.GetNotes;
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
                new CreateNoteCommand(GetUserId(user), request.Title, ToDomainContent(request.Content)), cancellationToken);
            return Results.Created($"/api/notes/{id}", id);
        });

        notes.MapPut("/{id:guid}", async (
            Guid id, UpdateNoteRequest request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var updated = await dispatcher.SendAsync(
                new UpdateNoteCommand(GetUserId(user), id, request.Title, ToDomainContent(request.Content)), cancellationToken);
            return updated ? Results.NoContent() : Results.NotFound();
        });

        notes.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var deleted = await dispatcher.SendAsync(new DeleteNoteCommand(GetUserId(user), id), cancellationToken);
            return deleted ? Results.NoContent() : Results.NotFound();
        });

        // Offers a copy of an owned note to another user - see ShareNoteCommand. The client is
        // responsible for notifying the recipient (a chat message carrying the returned share id),
        // since only the browser holds the key material to encrypt that message - mirrors
        // CalendarEndpoints' equivalent share endpoint.
        notes.MapPost("/{id:guid}/shares", async (
            Guid id, ShareNoteRequest request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var outcome = await dispatcher.SendAsync(
                new ShareNoteCommand(GetUserId(user), id, request.RecipientUserId, Enum.Parse<ShareAccessLevel>(request.AccessLevel, ignoreCase: true)),
                cancellationToken);
            return outcome is null ? Results.NotFound() : Results.Ok(new ShareResultDto(outcome.ShareId, outcome.AlreadyShared));
        });

        // Resolves a share offered to the caller into a copy in their own notes - see AcceptNoteShareCommand.
        notes.MapPost("/shares/{shareId:guid}/accept", async (
            Guid shareId, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var accepted = await dispatcher.SendAsync(new AcceptNoteShareCommand(GetUserId(user), shareId), cancellationToken);
            return accepted ? Results.NoContent() : Results.NotFound();
        });

        // Lets Chat.razor show an accurate "Accept" vs. "already accepted" state for a note-share message
        // even after a page reload, instead of only remembering what was clicked this session.
        notes.MapGet("/shares/{shareId:guid}/status", async (
            Guid shareId, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var isAccepted = await dispatcher.SendAsync(new GetNoteShareStatusQuery(GetUserId(user), shareId), cancellationToken);
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

    private static IReadOnlyList<NoteContentLine> ToDomainContent(IReadOnlyList<NoteContentLineDto> content)
        => content.Select(line => new NoteContentLine(line.Text, line.IsChecklistItem, line.IsChecked)).ToList();

    private static NoteContentLineDto ToDto(NoteContentLine line)
        => new(line.Text, line.IsChecklistItem, line.IsChecked);

    private static NoteDto ToDto(Note note)
        => new(note.Id, note.Title, note.Content.Select(ToDto).ToList(), note.CreatedAtUtc, note.UpdatedAtUtc,
            note.IsShared, note.SharedByUserName, note.AccessLevel.ToString(), note.OriginalOwnerUserId);
}
