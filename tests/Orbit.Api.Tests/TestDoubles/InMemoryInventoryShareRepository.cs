using Orbit.Core.Inventories;

namespace Orbit.Api.Tests.TestDoubles;

/// <summary>In-memory <see cref="IInventoryShareRepository"/> stub for unit tests - mirrors InMemoryNoteShareRepository.</summary>
internal sealed class InMemoryInventoryShareRepository : IInventoryShareRepository
{
    private readonly List<InventoryShare> _shares = [];

    public Task AddAsync(InventoryShare share, CancellationToken cancellationToken)
    {
        _shares.Add(share);
        return Task.CompletedTask;
    }

    public Task<InventoryShare?> GetByIdAsync(Guid recipientUserId, Guid id, CancellationToken cancellationToken)
        => Task.FromResult(_shares.FirstOrDefault(share => share.Id == id && share.RecipientUserId == recipientUserId));

    public Task UpdateAsync(InventoryShare share, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<InventoryShare?> FindExistingAsync(Guid sourceInventoryId, Guid recipientUserId, CancellationToken cancellationToken)
        => Task.FromResult(_shares.FirstOrDefault(
            share => share.SourceInventoryId == sourceInventoryId && share.RecipientUserId == recipientUserId));

    public Task<InventoryShare?> FindAcceptedGrantAsync(Guid sourceInventoryId, Guid recipientUserId, CancellationToken cancellationToken)
        => Task.FromResult(_shares.FirstOrDefault(
            share => share.SourceInventoryId == sourceInventoryId && share.RecipientUserId == recipientUserId && share.IsAccepted));

    public Task<IReadOnlyList<InventoryShare>> GetAcceptedGrantsForRecipientAsync(Guid recipientUserId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<InventoryShare>>(
            _shares.Where(share => share.RecipientUserId == recipientUserId && share.IsAccepted).ToList());

    public Task<IReadOnlySet<Guid>> GetSharedOutInventoryIdsAsync(Guid ownerUserId, CancellationToken cancellationToken)
    {
        IReadOnlySet<Guid> ids = _shares
            .Where(share => share.OwnerUserId == ownerUserId && share.IsAccepted)
            .Select(share => share.SourceInventoryId)
            .ToHashSet();

        return Task.FromResult(ids);
    }

    public Task RemoveAcceptedGrantAsync(Guid sourceId, Guid recipientUserId, CancellationToken cancellationToken)
    {
        _shares.RemoveAll(share =>
            share.SourceInventoryId == sourceId && share.RecipientUserId == recipientUserId && share.IsAccepted);
        return Task.CompletedTask;
    }
}
