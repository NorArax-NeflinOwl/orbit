using Orbit.Core.Abstractions;
using Orbit.Core.Inventory;

namespace Orbit.Core.Tasks;

/// <summary>
/// What a shared task list drags along with it: the lists its items link to, all the way down, and the
/// warehouse any of those lists is measured against.
///
/// A group list is a set of headings pointing at other lists, so handing one over on its own hands the
/// recipient a page on which nothing opens - and the same is true of the inventory its stock check is
/// read against. One grant is therefore offered by name and the rest follow it: created when it is
/// created, and accepted when it is accepted.
///
/// Private lists and private warehouses are left out. Their contents are sealed in their owner's
/// browser, so a grant of one would only ever hand the recipient ciphertext - the same reason
/// ShareTaskListCommandHandler refuses to share a private list at all.
/// </summary>
public sealed class TaskListShareCascade
{
    private readonly ITaskRepository _taskRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly ITaskListShareRepository _taskListShareRepository;
    private readonly IWarehouseShareRepository _warehouseShareRepository;

    public TaskListShareCascade(
        ITaskRepository taskRepository, IWarehouseRepository warehouseRepository,
        ITaskListShareRepository taskListShareRepository, IWarehouseShareRepository warehouseShareRepository)
    {
        _taskRepository = taskRepository;
        _warehouseRepository = warehouseRepository;
        _taskListShareRepository = taskListShareRepository;
        _warehouseShareRepository = warehouseShareRepository;
    }

    /// <summary>
    /// Grants everything below <paramref name="rootTaskListId"/> to the same recipient, at the level the
    /// root itself was granted at. A recipient who already holds one of them keeps what they have,
    /// raised to this level if it is higher - the same rule a second offer of the root follows.
    /// </summary>
    /// <param name="acceptImmediately">
    /// True where the root grant needs no answer either - a claimed public link is asked for by the
    /// person claiming it, so there is nothing left to agree to (see ClaimPublicShareLinkCommandHandler).
    /// </param>
    public async Task GrantAsync(
        Guid ownerUserId, Guid rootTaskListId, Guid recipientUserId, ShareAccessLevel accessLevel,
        bool acceptImmediately, CancellationToken cancellationToken)
    {
        var cascade = await ResolveAsync(ownerUserId, rootTaskListId, cancellationToken);

        foreach (var taskListId in cascade.TaskListIds)
        {
            await GrantTaskListAsync(ownerUserId, taskListId, recipientUserId, accessLevel, acceptImmediately, cancellationToken);
        }

        foreach (var warehouseId in cascade.WarehouseIds)
        {
            await GrantWarehouseAsync(ownerUserId, warehouseId, recipientUserId, accessLevel, acceptImmediately, cancellationToken);
        }
    }

    /// <summary>
    /// Accepts every grant that followed <paramref name="rootTaskListId"/>, so answering the one offer
    /// opens the whole tree rather than leaving the linked lists pending behind it.
    /// </summary>
    public async Task AcceptAsync(
        Guid ownerUserId, Guid rootTaskListId, Guid recipientUserId, CancellationToken cancellationToken)
    {
        var cascade = await ResolveAsync(ownerUserId, rootTaskListId, cancellationToken);

        foreach (var taskListId in cascade.TaskListIds)
        {
            if (await _taskListShareRepository.FindExistingAsync(taskListId, recipientUserId, cancellationToken) is not { IsAccepted: false } share)
            {
                continue;
            }

            share.MarkAccepted();
            await _taskListShareRepository.UpdateAsync(share, cancellationToken);
        }

        foreach (var warehouseId in cascade.WarehouseIds)
        {
            if (await _warehouseShareRepository.FindExistingAsync(warehouseId, recipientUserId, cancellationToken) is not { IsAccepted: false } share)
            {
                continue;
            }

            share.MarkAccepted();
            await _warehouseShareRepository.UpdateAsync(share, cancellationToken);
        }
    }

    /// <summary>
    /// The linked lists and warehouses that travel with the root, without the root itself - it is
    /// granted by its own handler, which is also what decides whether it may be granted at all.
    /// </summary>
    private async Task<CascadedItems> ResolveAsync(Guid ownerUserId, Guid rootTaskListId, CancellationToken cancellationToken)
    {
        var root = await _taskRepository.GetByIdAsync(ownerUserId, rootTaskListId, cancellationToken);
        if (root is null)
        {
            // Reached through a share rather than owned - the linked lists then belong to someone whose
            // lists this caller cannot enumerate, so there is nothing here to pass on.
            return CascadedItems.None;
        }

        var owned = await _taskRepository.GetAllAsync(ownerUserId, updatedSinceUtc: null, cancellationToken);
        var tree = StockCheck.LinkedTaskListTree.Flatten(root, owned);
        var linkedTaskLists = tree.Where(taskList => taskList.Id != rootTaskListId && !taskList.IsPrivate).ToList();

        var warehouseIds = new List<Guid>();
        foreach (var candidateId in tree.Select(taskList => taskList.LinkedWarehouseId).OfType<Guid>().Distinct())
        {
            if (await _warehouseRepository.GetByIdAsync(ownerUserId, candidateId, cancellationToken) is { IsPrivate: false })
            {
                warehouseIds.Add(candidateId);
            }
        }

        return new CascadedItems([.. linkedTaskLists.Select(taskList => taskList.Id)], warehouseIds);
    }

    private async Task GrantTaskListAsync(
        Guid ownerUserId, Guid taskListId, Guid recipientUserId, ShareAccessLevel accessLevel,
        bool acceptImmediately, CancellationToken cancellationToken)
    {
        if (await _taskListShareRepository.FindExistingAsync(taskListId, recipientUserId, cancellationToken) is { } existing)
        {
            if (existing.RaiseAccessLevelTo(accessLevel))
            {
                await _taskListShareRepository.UpdateAsync(existing, cancellationToken);
            }

            return;
        }

        var share = TaskListShare.Create(taskListId, ownerUserId, recipientUserId, accessLevel);
        if (acceptImmediately)
        {
            share.MarkAccepted();
        }

        await _taskListShareRepository.AddAsync(share, cancellationToken);
    }

    private async Task GrantWarehouseAsync(
        Guid ownerUserId, Guid warehouseId, Guid recipientUserId, ShareAccessLevel accessLevel,
        bool acceptImmediately, CancellationToken cancellationToken)
    {
        if (await _warehouseShareRepository.FindExistingAsync(warehouseId, recipientUserId, cancellationToken) is { } existing)
        {
            if (existing.RaiseAccessLevelTo(accessLevel))
            {
                await _warehouseShareRepository.UpdateAsync(existing, cancellationToken);
            }

            return;
        }

        var share = WarehouseShare.Create(warehouseId, ownerUserId, recipientUserId, accessLevel);
        if (acceptImmediately)
        {
            share.MarkAccepted();
        }

        await _warehouseShareRepository.AddAsync(share, cancellationToken);
    }

    /// <summary>The ids that follow one shared task list, gathered once and then granted or accepted.</summary>
    private sealed record CascadedItems(IReadOnlyList<Guid> TaskListIds, IReadOnlyList<Guid> WarehouseIds)
    {
        public static readonly CascadedItems None = new([], []);
    }
}
