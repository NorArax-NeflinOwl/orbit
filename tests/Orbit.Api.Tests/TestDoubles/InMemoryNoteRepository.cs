using Orbit.Core.Notes;

namespace Orbit.Api.Tests.TestDoubles;

/// <summary>
/// In-memory <see cref="INoteRepository"/> stub for unit tests that need real add/lookup/update
/// behavior, including per-user ownership scoping, without spinning up SQLite.
/// </summary>
internal sealed class InMemoryNoteRepository : INoteRepository
{
    private readonly List<Note> _notes = [];

    public Task<IReadOnlyList<Note>> GetAllAsync(
        Guid userId, DateTimeOffset? updatedSinceUtc, CancellationToken cancellationToken)
    {
        var matching = _notes.Where(note => note.UserId == userId);
        if (updatedSinceUtc is not null)
        {
            matching = matching.Where(note => note.UpdatedAtUtc >= updatedSinceUtc.Value);
        }

        return Task.FromResult<IReadOnlyList<Note>>(matching.ToList());
    }

    public Task<Note?> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken)
        => Task.FromResult(_notes.FirstOrDefault(note => note.Id == id && note.UserId == userId));

    public Task AddAsync(Note note, CancellationToken cancellationToken)
    {
        _notes.Add(note);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Note note, CancellationToken cancellationToken)
    {
        // Handlers mutate the same Note instance this repository already holds a reference to, so
        // there is nothing to replace here - this mirrors how the EF Core repository just calls
        // SaveChangesAsync on an already-tracked entity.
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken)
    {
        _notes.RemoveAll(note => note.Id == id && note.UserId == userId);
        return Task.CompletedTask;
    }
}
