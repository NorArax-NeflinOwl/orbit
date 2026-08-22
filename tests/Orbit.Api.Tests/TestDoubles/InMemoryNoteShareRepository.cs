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

    public Task UpdateAsync(NoteShare share, CancellationToken cancellationToken)
    {
        // Handlers mutate the same NoteShare instance this repository already holds a reference to, so
        // there is nothing to replace here - mirrors InMemoryCalendarEventShareRepository.
        return Task.CompletedTask;
    }
}
