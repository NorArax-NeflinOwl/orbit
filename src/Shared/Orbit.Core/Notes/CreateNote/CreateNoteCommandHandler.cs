using Orbit.Core.Abstractions;

namespace Orbit.Core.Notes.CreateNote;

public sealed class CreateNoteCommandHandler : IRequestHandler<CreateNoteCommand, Guid>
{
    private readonly INoteRepository _noteRepository;

    public CreateNoteCommandHandler(INoteRepository noteRepository)
    {
        _noteRepository = noteRepository;
    }

    public async Task<Guid> HandleAsync(CreateNoteCommand request, CancellationToken cancellationToken)
    {
        var note = Note.Create(request.Title, request.Content);
        await _noteRepository.AddAsync(note, cancellationToken);
        return note.Id;
    }
}
