using Orbit.Core.Abstractions;

namespace Orbit.Core.Notes.GetNotes;

public sealed class GetNotesQueryHandler : IRequestHandler<GetNotesQuery, IReadOnlyList<Note>>
{
    private readonly INoteRepository _noteRepository;

    public GetNotesQueryHandler(INoteRepository noteRepository)
    {
        _noteRepository = noteRepository;
    }

    public Task<IReadOnlyList<Note>> HandleAsync(GetNotesQuery request, CancellationToken cancellationToken)
        => _noteRepository.GetAllAsync(request.UserId, cancellationToken);
}
