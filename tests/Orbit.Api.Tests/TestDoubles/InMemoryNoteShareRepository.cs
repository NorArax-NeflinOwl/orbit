using Orbit.Core.Notes;

namespace Orbit.Api.Tests.TestDoubles;

/// <summary>
/// In-memory <see cref="INoteShareRepository"/> stub for unit tests that need real add/lookup/update
/// behavior, including per-recipient scoping, without spinning up SQLite. Mirrors InMemoryCalendarEventShareRepository.
/// </summary>
internal sealed class InMemoryNoteShareRepository : INoteShareRepository
{
    private readonly List<NoteShare> _shares = [];

    public Task AddAsync(NoteShare share, CancellationToken cancellationToken)
    {
        _shares.Add(share);
        return Task.CompletedTask;
    }

    public Task<NoteShare?> GetByIdAsync(Guid recipientUserId, Guid id, CancellationToken cancellationToken)
        => Task.FromResult(_shares.FirstOrDefault(share => share.Id == id && share.RecipientUserId == recipientUserId));

    public Task<NoteShare?> FindExistingAsync(Guid sourceNoteId, Guid recipientUserId, CancellationToken cancellationToken)
        => Task.FromResult(_shares.FirstOrDefault(share => share.SourceNoteId == sourceNoteId && share.RecipientUserId == recipientUserId));

    public Task<NoteShare?> FindAcceptedGrantAsync(Guid sourceNoteId, Guid recipientUserId, CancellationToken cancellationToken)
        => Task.FromResult(_shares.FirstOrDefault(
            share => share.SourceNoteId == sourceNoteId && share.RecipientUserId == recipientUserId && share.IsAccepted));

    public Task<IReadOnlyList<NoteShare>> GetAcceptedGrantsForRecipientAsync(Guid recipientUserId, CancellationToken cancellationToken)
    {
        IReadOnlyList<NoteShare> grants = _shares.Where(share => share.RecipientUserId == recipientUserId && share.IsAccepted).ToList();
        return Task.FromResult(grants);
    }

    public Task<IReadOnlySet<Guid>> GetSharedOutNoteIdsAsync(Guid ownerUserId, CancellationToken cancellationToken)
    {
        IReadOnlySet<Guid> noteIds = _shares
            .Where(share => share.OwnerUserId == ownerUserId && share.IsAccepted)
            .Select(share => share.SourceNoteId)
            .ToHashSet();

        return Task.FromResult(noteIds);
    }

    public Task UpdateAsync(NoteShare share, CancellationToken cancellationToken)
    {
        // Handlers mutate the same NoteShare instance this repository already holds a reference to, so
        // there is nothing to replace here - mirrors InMemoryCalendarEventShareRepository.
        return Task.CompletedTask;
    }
    public Task RemoveAcceptedGrantAsync(Guid sourceId, Guid recipientUserId, CancellationToken cancellationToken)
    {
        _shares.RemoveAll(share =>
            share.SourceNoteId == sourceId && share.RecipientUserId == recipientUserId && share.IsAccepted);
        return Task.CompletedTask;
    }
}