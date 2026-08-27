using Orbit.Core.Abstractions;

namespace Orbit.Core.Notes.SetNotePinned;

/// <summary>
/// Only the note's owner can pin it. Pinning is about where a note sits on one person's own page, so a
/// recipient pinning a note shared with them would be moving it for its owner instead of for themselves -
/// a per-reader pin is a different feature, and a worse one to arrive at by accident. Mirrors
/// SetTaskListPinnedCommandHandler.
/// </summary>
public sealed class SetNotePinnedCommandHandler : IRequestHandler<SetNotePinnedCommand, bool>
{
    private readonly INoteRepository _noteRepository;

    public SetNotePinnedCommandHandler(INoteRepository noteRepository)
    {
        _noteRepository = noteRepository;
    }

    public async Task<bool> HandleAsync(SetNotePinnedCommand request, CancellationToken cancellationToken)
    {
        var note = await _noteRepository.GetByIdAsync(request.UserId, request.NoteId, cancellationToken);
        if (note is null || note.UserId != request.UserId)
        {
            return false;
        }

        note.SetPinned(request.IsPinned);
        await _noteRepository.UpdateAsync(note, cancellationToken);
        return true;
    }
}
