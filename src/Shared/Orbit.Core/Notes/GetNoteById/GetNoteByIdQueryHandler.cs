using Orbit.Core.Abstractions;

namespace Orbit.Core.Notes.GetNoteById;

public sealed class GetNoteByIdQueryHandler : IRequestHandler<GetNoteByIdQuery, Note?>
{
    private readonly NoteAccessResolver _noteAccessResolver;

    public GetNoteByIdQueryHandler(NoteAccessResolver noteAccessResolver)
    {
        _noteAccessResolver = noteAccessResolver;
    }

    public Task<Note?> HandleAsync(GetNoteByIdQuery request, CancellationToken cancellationToken)
        => _noteAccessResolver.ResolveAsync(request.UserId, request.Id, cancellationToken);
}
