using Orbit.Core.Abstractions;

namespace Orbit.Core.Notes.GetNoteById;

public sealed class GetNoteByIdQueryHandler : IRequestHandler<GetNoteByIdQuery, Note?>
{
    private readonly INoteRepository _noteRepository;

    public GetNoteByIdQueryHandler(INoteRepository noteRepository)
    {
        _noteRepository = noteRepository;
    }

    public Task<Note?> HandleAsync(GetNoteByIdQuery request, CancellationToken cancellationToken)
        => _noteRepository.GetByIdAsync(request.Id, cancellationToken);
}
