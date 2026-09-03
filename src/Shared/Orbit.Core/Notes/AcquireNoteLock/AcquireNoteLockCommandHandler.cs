using Orbit.Core.Abstractions;
using Orbit.Core.Users;

namespace Orbit.Core.Notes.AcquireNoteLock;

public sealed class AcquireNoteLockCommandHandler : IRequestHandler<AcquireNoteLockCommand, EditOutcome>
{
    /// <summary>
    /// How long an acquired lock stays valid without a refresh - long enough that NoteEditor.razor's
    /// heartbeat (see its class comment) comfortably renews it well before expiry under normal network
    /// conditions, short enough that a genuinely abandoned lock (closed tab, crashed browser, lost
    /// connection) doesn't block the note for an unreasonable stretch of time.
    /// </summary>
    private static readonly TimeSpan LockDuration = TimeSpan.FromSeconds(60);

    private readonly NoteAccessResolver _noteAccessResolver;
    private readonly INoteRepository _noteRepository;
    private readonly IUserRepository _userRepository;

    public AcquireNoteLockCommandHandler(NoteAccessResolver noteAccessResolver, INoteRepository noteRepository, IUserRepository userRepository)
    {
        _noteAccessResolver = noteAccessResolver;
        _noteRepository = noteRepository;
        _userRepository = userRepository;
    }

    public async Task<EditOutcome> HandleAsync(AcquireNoteLockCommand request, CancellationToken cancellationToken)
    {
        var note = await _noteAccessResolver.ResolveAsync(request.UserId, request.NoteId, cancellationToken);
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

        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        note.AcquireLock(request.UserId, user!.UserName, nowUtc, LockDuration);
        await _noteRepository.UpdateLockAsync(note, cancellationToken);
        return EditOutcome.Success;
    }
}
