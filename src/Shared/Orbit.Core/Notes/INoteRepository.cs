namespace Orbit.Core.Notes;

public interface INoteRepository
{
    Task<IReadOnlyList<Note>> GetAllAsync(Guid userId, CancellationToken cancellationToken);

    Task<Note?> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken);

    Task AddAsync(Note note, CancellationToken cancellationToken);

    Task UpdateAsync(Note note, CancellationToken cancellationToken);
}
