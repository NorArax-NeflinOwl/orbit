using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Orbit.Contracts.Notes;
using Orbit.Core.Abstractions;
using Orbit.Core.Notes;
using Orbit.Core.Notes.CreateNote;
using Orbit.Core.Notes.GetNoteById;
using Orbit.Core.Notes.GetNotes;
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
                new CreateNoteCommand(GetUserId(user), request.Title, request.Content), cancellationToken);
            return Results.Created($"/api/notes/{id}", id);
        });

        notes.MapPut("/{id:guid}", async (
            Guid id, UpdateNoteRequest request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var updated = await dispatcher.SendAsync(
                new UpdateNoteCommand(GetUserId(user), id, request.Title, request.Content), cancellationToken);
            return updated ? Results.NoContent() : Results.NotFound();
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

    private static NoteDto ToDto(Note note)
        => new(note.Id, note.Title, note.Content, note.CreatedAtUtc, note.UpdatedAtUtc);
}
