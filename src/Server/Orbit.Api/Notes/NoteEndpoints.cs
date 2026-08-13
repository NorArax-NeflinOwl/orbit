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
        var notes = app.MapGroup("/api/notes");

        notes.MapGet("/", async (IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(new GetNotesQuery(), cancellationToken);
            return Results.Ok(result.Select(ToDto));
        });

        notes.MapGet("/{id:guid}", async (Guid id, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var note = await dispatcher.SendAsync(new GetNoteByIdQuery(id), cancellationToken);
            return note is null ? Results.NotFound() : Results.Ok(ToDto(note));
        });

        notes.MapPost("/", async (CreateNoteRequest request, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var id = await dispatcher.SendAsync(new CreateNoteCommand(request.Title, request.Content), cancellationToken);
            return Results.Created($"/api/notes/{id}", id);
        });

        notes.MapPut("/{id:guid}", async (Guid id, UpdateNoteRequest request, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var updated = await dispatcher.SendAsync(new UpdateNoteCommand(id, request.Title, request.Content), cancellationToken);
            return updated ? Results.NoContent() : Results.NotFound();
        });
    }

    private static NoteDto ToDto(Note note)
        => new(note.Id, note.Title, note.Content, note.CreatedAtUtc, note.UpdatedAtUtc);
}
