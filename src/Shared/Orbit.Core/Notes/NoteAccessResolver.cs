using Orbit.Core.Abstractions;
using Orbit.Core.Users;

namespace Orbit.Core.Notes;

/// <summary>
/// Loads a note the way a given caller is actually allowed to see it - either because they own it, or
/// because someone shared it with them (see NoteShare) - and stamps the result with that relationship
/// via Note.SetAccessContext. Every read path (GetNoteByIdQuery, GetNotesQuery) and every write/lock path
/// that needs to know "does this caller have access, and at what level" goes through here instead of
/// duplicating the owner-or-grant lookup.
/// </summary>
public sealed class NoteAccessResolver
{
    private readonly INoteRepository _noteRepository;
    private readonly INoteShareRepository _noteShareRepository;
    private readonly IUserRepository _userRepository;

    public NoteAccessResolver(INoteRepository noteRepository, INoteShareRepository noteShareRepository, IUserRepository userRepository)
    {
        _noteRepository = noteRepository;
        _noteShareRepository = noteShareRepository;
        _userRepository = userRepository;
    }

    /// <summary>Null when callerId neither owns noteId nor has an accepted share of it.</summary>
    public async Task<Note?> ResolveAsync(Guid callerId, Guid noteId, CancellationToken cancellationToken)
    {
        var ownedNote = await _noteRepository.GetByIdAsync(callerId, noteId, cancellationToken);
        if (ownedNote is not null)
        {
            return ownedNote;
        }

        var grant = await _noteShareRepository.FindAcceptedGrantAsync(noteId, callerId, cancellationToken);
        if (grant is null)
        {
            return null;
        }

        // The owner may have deleted the note after granting access but before this lookup - a dangling
        // grant reads as "not found" here rather than throwing, the same way any other stale reference in
        // this codebase (e.g. a calendar reminder claim for a deleted event) is left rather than actively cleaned up.
        var note = await _noteRepository.GetByIdAsync(grant.OwnerUserId, grant.SourceNoteId, cancellationToken);
        if (note is null)
        {
            return null;
        }

        var owner = await _userRepository.GetByIdAsync(grant.OwnerUserId, cancellationToken);
        note.SetAccessContext(isShared: true, owner?.UserName, grant.AccessLevel);
        return note;
    }

    /// <summary>Every note callerId owns, plus every note shared with them (accepted grants only) - see Notes.razor/Dashboard.razor.</summary>
    public async Task<IReadOnlyList<Note>> ResolveAllAsync(Guid callerId, CancellationToken cancellationToken)
    {
        var owned = await _noteRepository.GetAllAsync(callerId, cancellationToken);
        var grants = await _noteShareRepository.GetAcceptedGrantsForRecipientAsync(callerId, cancellationToken);

        var granted = new List<Note>();
        foreach (var grant in grants)
        {
            var note = await _noteRepository.GetByIdAsync(grant.OwnerUserId, grant.SourceNoteId, cancellationToken);
            if (note is null)
            {
                continue;
            }

            var owner = await _userRepository.GetByIdAsync(grant.OwnerUserId, cancellationToken);
            note.SetAccessContext(isShared: true, owner?.UserName, grant.AccessLevel);
            granted.Add(note);
        }

        return owned.Concat(granted).ToList();
    }
}
