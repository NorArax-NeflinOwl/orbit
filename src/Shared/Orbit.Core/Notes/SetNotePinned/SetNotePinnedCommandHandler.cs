using Orbit.Core.Abstractions;

namespace Orbit.Core.Notes.SetNotePinned;

/// <summary>
/// Where this note sits on the caller's own page - two rows behind one question, chosen by which of
/// them the caller has.
///
/// An owner's answer is the note's own IsPinned. A recipient's is on their grant
/// (<see cref="NoteShare.IsPinnedByRecipient"/>), because the note's belongs to whoever owns it and a
/// recipient writing there would be rearranging somebody else's page. This used to refuse a recipient
/// outright and the browser kept their answer in localStorage instead, where it did not follow them to
/// a second browser or to the phone.
///
/// Nobody else gets either: no grant and not the owner means there is nothing here to arrange.
/// Mirrors SetTaskListPinnedCommandHandler.
/// </summary>
public sealed class SetNotePinnedCommandHandler : IRequestHandler<SetNotePinnedCommand, bool>
{
    private readonly INoteRepository _noteRepository;
    private readonly INoteShareRepository _noteShareRepository;

    public SetNotePinnedCommandHandler(INoteRepository noteRepository, INoteShareRepository noteShareRepository)
    {
        _noteRepository = noteRepository;
        _noteShareRepository = noteShareRepository;
    }

    public async Task<bool> HandleAsync(SetNotePinnedCommand request, CancellationToken cancellationToken)
    {
        var note = await _noteRepository.GetByIdAsync(request.UserId, request.NoteId, cancellationToken);
        if (note is not null && note.UserId == request.UserId)
        {
            note.SetPinned(request.IsPinned);
            await _noteRepository.UpdateAsync(note, cancellationToken);
            return true;
        }

        var grant = await _noteShareRepository.FindAcceptedGrantAsync(
            request.NoteId, request.UserId, cancellationToken);
        if (grant is null)
        {
            return false;
        }

        // Answered true either way, including when nothing changed: the caller asked for a state and
        // that is the state it is in. A write is skipped, not the answer.
        if (grant.SetPinnedByRecipient(request.IsPinned))
        {
            await _noteShareRepository.UpdateAsync(grant, cancellationToken);
        }

        return true;
    }
}
