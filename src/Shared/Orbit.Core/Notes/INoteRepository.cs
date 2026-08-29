namespace Orbit.Core.Notes;

public interface INoteRepository
{
    /// <summary>
    /// Everything userId owns, or - when updatedSinceUtc is given - only what changed at or after it.
    /// The cursor is applied in the database: a client catching up asks for a delta, and answering it by
    /// fetching everything and discarding most of it saved the wire and nothing else.
    /// </summary>
    Task<IReadOnlyList<Note>> GetAllAsync(
        Guid userId, DateTimeOffset? updatedSinceUtc, CancellationToken cancellationToken);

    Task<Note?> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken);

    Task AddAsync(Note note, CancellationToken cancellationToken);

    Task UpdateAsync(Note note, CancellationToken cancellationToken);

    Task DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken);
}
