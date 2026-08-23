using Orbit.Core.Abstractions;

namespace Orbit.Core.Notes.GetNotes;

public sealed class GetNotesQueryHandler : IRequestHandler<GetNotesQuery, IReadOnlyList<Note>>
{
    private readonly NoteAccessResolver _noteAccessResolver;

    public GetNotesQueryHandler(NoteAccessResolver noteAccessResolver)
    {
        _noteAccessResolver = noteAccessResolver;
    }

    public Task<IReadOnlyList<Note>> HandleAsync(GetNotesQuery request, CancellationToken cancellationToken)
        => _noteAccessResolver.ResolveAllAsync(request.UserId, cancellationToken);
}
