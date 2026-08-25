namespace Orbit.Core.Location;

public interface ISharedLocationRepository
{
    /// <summary>The single row for this pair, if one exists - see SharedLocation's comment on why there is only ever one.</summary>
    Task<SharedLocation?> FindAsync(Guid sharerUserId, Guid recipientUserId, CancellationToken cancellationToken);

    Task AddAsync(SharedLocation sharedLocation, CancellationToken cancellationToken);

    Task UpdateAsync(SharedLocation sharedLocation, CancellationToken cancellationToken);

    /// <summary>Every position currently shared *with* recipientUserId, most recently updated first.</summary>
    Task<IReadOnlyList<SharedLocation>> GetSharedWithAsync(Guid recipientUserId, CancellationToken cancellationToken);

    /// <summary>Every position sharerUserId is currently sharing, so they can see and end each one.</summary>
    Task<IReadOnlyList<SharedLocation>> GetSharedByAsync(Guid sharerUserId, CancellationToken cancellationToken);

    /// <summary>Removes the row for this pair. No-op when nothing is shared - stopping twice is not an error.</summary>
    Task DeleteAsync(Guid sharerUserId, Guid recipientUserId, CancellationToken cancellationToken);

    /// <summary>Removes every position sharerUserId is sharing - what "stop sharing with everyone" means.</summary>
    Task DeleteAllBySharerAsync(Guid sharerUserId, CancellationToken cancellationToken);
}
