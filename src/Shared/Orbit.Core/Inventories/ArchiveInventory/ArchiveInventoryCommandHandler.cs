using Orbit.Core.Abstractions;
using Orbit.Core.Tasks;

namespace Orbit.Core.Inventories.ArchiveInventory;

/// <summary>
/// Only the owner puts a shelf away, and only their own. A recipient must not: one row is one shelf, so
/// archiving one shared with them would take it off its owner's page - the same reason they cannot file
/// or pin one. Mirrors MoveInventoryToFolderCommandHandler on who may.
/// </summary>
public sealed class ArchiveInventoryCommandHandler : IRequestHandler<ArchiveInventoryCommand, bool>
{
    private readonly IInventoryRepository _inventories;
    private readonly ITaskRepository _taskRepository;

    public ArchiveInventoryCommandHandler(IInventoryRepository inventories, ITaskRepository taskRepository)
    {
        _inventories = inventories;
        _taskRepository = taskRepository;
    }

    public async Task<bool> HandleAsync(ArchiveInventoryCommand request, CancellationToken cancellationToken)
    {
        var found = await _inventories.GetByIdAsync(request.UserId, request.InventoryId, cancellationToken);
        if (found is null || found.UserId != request.UserId)
        {
            return false;
        }

        found.Archive(request.IsArchived);
        await _inventories.UpdateAsync(found, cancellationToken);
        if (request.IsArchived)
        {
            await LetTheListsGoAsync(request, cancellationToken);
        }

        return true;
    }

    /// <summary>
    /// Takes the shelf off every list that was measured against it. Asked for on 2026-09-18: putting a
    /// shelf away is saying it is done with, and a list that went on being measured against it kept
    /// showing a stock check against a shelf its owner had filed out of sight - and went on raising
    /// restock errands from it.
    ///
    /// Only on the way in. Bringing a shelf back does not put the links back, because nothing records
    /// which lists they were: a list is pointed at a shelf by somebody choosing it, and guessing would
    /// be inventing a choice they did not make. The lists are still there, and choosing it again is one
    /// press - see LinkTaskListToInventoryCommand.
    /// </summary>
    private async Task LetTheListsGoAsync(ArchiveInventoryCommand request, CancellationToken cancellationToken)
    {
        var measuredAgainstIt = (await _taskRepository.GetAllAsync(request.UserId, updatedSinceUtc: null, cancellationToken))
            .Where(taskList => taskList.LinkedInventoryId == request.InventoryId)
            .ToList();
        if (measuredAgainstIt.Count == 0)
        {
            return;
        }

        foreach (var taskList in measuredAgainstIt)
        {
            taskList.LinkToInventory(null);
        }

        await _taskRepository.UpdateManyAsync(measuredAgainstIt, cancellationToken);
    }
}
