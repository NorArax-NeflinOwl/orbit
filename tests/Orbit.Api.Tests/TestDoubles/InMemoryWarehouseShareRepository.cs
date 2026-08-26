using Orbit.Core.Inventory;

namespace Orbit.Api.Tests.TestDoubles;

/// <summary>In-memory <see cref="IWarehouseShareRepository"/> stub for unit tests - mirrors InMemoryNoteShareRepository.</summary>
internal sealed class InMemoryWarehouseShareRepository : IWarehouseShareRepository
{
    private readonly List<WarehouseShare> _shares = [];

    public Task AddAsync(WarehouseShare share, CancellationToken cancellationToken)
    {
        _shares.Add(share);
        return Task.CompletedTask;
    }

    public Task<WarehouseShare?> GetByIdAsync(Guid recipientUserId, Guid id, CancellationToken cancellationToken)
        => Task.FromResult(_shares.FirstOrDefault(share => share.Id == id && share.RecipientUserId == recipientUserId));

    public Task UpdateAsync(WarehouseShare share, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<WarehouseShare?> FindExistingAsync(Guid sourceWarehouseId, Guid recipientUserId, CancellationToken cancellationToken)
        => Task.FromResult(_shares.FirstOrDefault(
            share => share.SourceWarehouseId == sourceWarehouseId && share.RecipientUserId == recipientUserId));

    public Task<WarehouseShare?> FindAcceptedGrantAsync(Guid sourceWarehouseId, Guid recipientUserId, CancellationToken cancellationToken)
        => Task.FromResult(_shares.FirstOrDefault(
            share => share.SourceWarehouseId == sourceWarehouseId && share.RecipientUserId == recipientUserId && share.IsAccepted));

    public Task<IReadOnlyList<WarehouseShare>> GetAcceptedGrantsForRecipientAsync(Guid recipientUserId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<WarehouseShare>>(
            _shares.Where(share => share.RecipientUserId == recipientUserId && share.IsAccepted).ToList());

    public Task<IReadOnlySet<Guid>> GetSharedOutWarehouseIdsAsync(Guid ownerUserId, CancellationToken cancellationToken)
    {
        IReadOnlySet<Guid> ids = _shares
            .Where(share => share.OwnerUserId == ownerUserId && share.IsAccepted)
            .Select(share => share.SourceWarehouseId)
            .ToHashSet();

        return Task.FromResult(ids);
    }
}
