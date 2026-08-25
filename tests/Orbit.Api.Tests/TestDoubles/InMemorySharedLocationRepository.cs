using Orbit.Core.Location;

namespace Orbit.Api.Tests.TestDoubles;

internal sealed class InMemorySharedLocationRepository : ISharedLocationRepository
{
    private readonly List<SharedLocation> _sharedLocations = [];

    public Task<SharedLocation?> FindAsync(Guid sharerUserId, Guid recipientUserId, CancellationToken cancellationToken)
        => Task.FromResult(_sharedLocations.FirstOrDefault(
            shared => shared.SharerUserId == sharerUserId && shared.RecipientUserId == recipientUserId));

    public Task AddAsync(SharedLocation sharedLocation, CancellationToken cancellationToken)
    {
        _sharedLocations.Add(sharedLocation);
        return Task.CompletedTask;
    }

    /// <summary>Handlers mutate the same instance this repository holds - mirrors the other in-memory doubles.</summary>
    public Task UpdateAsync(SharedLocation sharedLocation, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<IReadOnlyList<SharedLocation>> GetSharedWithAsync(Guid recipientUserId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<SharedLocation>>(
            _sharedLocations
                .Where(shared => shared.RecipientUserId == recipientUserId)
                .OrderByDescending(shared => shared.UpdatedAtUtc)
                .ToList());

    public Task<IReadOnlyList<SharedLocation>> GetSharedByAsync(Guid sharerUserId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<SharedLocation>>(
            _sharedLocations
                .Where(shared => shared.SharerUserId == sharerUserId)
                .OrderByDescending(shared => shared.UpdatedAtUtc)
                .ToList());

    public Task DeleteAsync(Guid sharerUserId, Guid recipientUserId, CancellationToken cancellationToken)
    {
        _sharedLocations.RemoveAll(shared => shared.SharerUserId == sharerUserId && shared.RecipientUserId == recipientUserId);
        return Task.CompletedTask;
    }

    public Task DeleteAllBySharerAsync(Guid sharerUserId, CancellationToken cancellationToken)
    {
        _sharedLocations.RemoveAll(shared => shared.SharerUserId == sharerUserId);
        return Task.CompletedTask;
    }
}
