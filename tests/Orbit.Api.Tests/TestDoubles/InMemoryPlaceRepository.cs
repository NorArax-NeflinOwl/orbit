using Orbit.Core.Places;

namespace Orbit.Api.Tests.TestDoubles;

/// <summary>
/// In-memory <see cref="IPlaceRepository"/> stub - mirrors InMemoryNoteRepository, including the
/// per-owner scoping, which is the whole of a place's access control.
/// </summary>
internal sealed class InMemoryPlaceRepository : IPlaceRepository
{
    private readonly List<Place> _places = [];

    public Task<IReadOnlyList<Place>> GetAllAsync(
        Guid userId, DateTimeOffset? updatedSinceUtc, CancellationToken cancellationToken)
    {
        var matching = _places.Where(place => place.UserId == userId);
        if (updatedSinceUtc is not null)
        {
            matching = matching.Where(place => place.UpdatedAtUtc >= updatedSinceUtc.Value);
        }

        return Task.FromResult<IReadOnlyList<Place>>(matching.ToList());
    }

    public Task<Place?> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken)
        => Task.FromResult(_places.FirstOrDefault(place => place.Id == id && place.UserId == userId));

    public Task AddAsync(Place place, CancellationToken cancellationToken)
    {
        _places.Add(place);
        return Task.CompletedTask;
    }

    /// <inheritdoc cref="InMemoryNoteRepository.UpdateAsync"/>
    public Task UpdateAsync(Place place, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<bool> DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken)
        => Task.FromResult(_places.RemoveAll(place => place.Id == id && place.UserId == userId) > 0);
}
