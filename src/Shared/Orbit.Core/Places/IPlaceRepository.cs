namespace Orbit.Core.Places;

/// <summary>Mirrors INoteRepository - see it for why the cursor is applied in the database.</summary>
public interface IPlaceRepository
{
    /// <inheritdoc cref="Orbit.Core.Notes.INoteRepository.GetAllAsync"/>
    Task<IReadOnlyList<Place>> GetAllAsync(
        Guid userId, DateTimeOffset? updatedSinceUtc, CancellationToken cancellationToken);

    Task<Place?> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken);

    Task AddAsync(Place place, CancellationToken cancellationToken);

    Task UpdateAsync(Place place, CancellationToken cancellationToken);

    /// <summary>False when there was nothing of this user's to delete, which is how a stale id reads.</summary>
    Task<bool> DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken);
}
