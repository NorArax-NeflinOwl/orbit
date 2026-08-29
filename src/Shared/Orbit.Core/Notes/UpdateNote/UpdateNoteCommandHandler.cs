using Orbit.Core.Abstractions;

namespace Orbit.Core.Notes.UpdateNote;

public sealed class UpdateNoteCommandHandler : IRequestHandler<UpdateNoteCommand, EditOutcome>
{
    private readonly NoteAccessResolver _noteAccessResolver;
    private readonly INoteRepository _noteRepository;

    public UpdateNoteCommandHandler(NoteAccessResolver noteAccessResolver, INoteRepository noteRepository)
    {
        _noteAccessResolver = noteAccessResolver;
        _noteRepository = noteRepository;
    }

    /// <summary>
    /// NotFound covers the note missing, not accessible to the caller, or accessible only at ReadOnly/Share
    /// (a shared copy without CanEdit access can't be edited) - the API turns all of those into a 404,
    /// without leaking which one applies. Locked means someone else currently holds the edit lock (see
    /// Note.AcquireLock) - the client is expected to have acquired it itself before ever showing an
    /// editable form, so this is a defense-in-depth check against a lock that was acquired or expired
    /// between page load and clicking Save, not the primary way a user finds out about a lock.
    /// </summary>
    public async Task<EditOutcome> HandleAsync(UpdateNoteCommand request, CancellationToken cancellationToken)
    {
        var note = await _noteAccessResolver.ResolveAsync(request.UserId, request.Id, cancellationToken);
        if (note is null)
        {
            return EditOutcome.NotFound;
        }

        // Visible but not theirs to change - see EditOutcomeKind.ReadOnly for why that is worth saying.
        if (!note.AccessLevel.AllowsEditing())
        {
            return EditOutcome.ReadOnly;
        }

        var nowUtc = DateTimeOffset.UtcNow;
        if (note.IsLockedByAnotherUser(request.UserId, nowUtc))
        {
            return EditOutcome.LockedBy(note.LockedByUserName!);
        }

        note.Update(request.Title, request.Content, request.IsPrivate, request.EncryptedContent, request.Priority);
        await _noteRepository.UpdateAsync(note, cancellationToken);
        return EditOutcome.Success;
    }
}
