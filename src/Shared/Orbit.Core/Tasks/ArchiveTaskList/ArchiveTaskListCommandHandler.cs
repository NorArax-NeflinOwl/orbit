using Orbit.Core.Abstractions;
using Orbit.Core.Inventories;

namespace Orbit.Core.Tasks.ArchiveTaskList;

/// <summary>
/// Only the owner puts a list away, and only their own. A recipient must not: one row is one list, so
/// archiving one shared with them would take it off its owner's page - the same reason they cannot file
/// or pin one. Mirrors MoveTaskListToFolderCommandHandler on who may.
/// </summary>
public sealed class ArchiveTaskListCommandHandler : IRequestHandler<ArchiveTaskListCommand, bool>
{
    private readonly ITaskRepository _taskLists;
    private readonly ShelfUsage? _shelfUsage;

    /// <param name="shelfUsage">
    /// Recounts what the shelves are asked for, since a row taken out of a group may have been asking -
    /// see ShelfUsage. Optional for the reason CreateTaskListCommandHandler gives.
    /// </param>
    public ArchiveTaskListCommandHandler(ITaskRepository taskLists, ShelfUsage? shelfUsage = null)
    {
        _taskLists = taskLists;
        _shelfUsage = shelfUsage;
    }

    public async Task<bool> HandleAsync(ArchiveTaskListCommand request, CancellationToken cancellationToken)
    {
        var found = await _taskLists.GetByIdAsync(request.UserId, request.TaskListId, cancellationToken);
        if (found is null || found.UserId != request.UserId)
        {
            return false;
        }

        found.Archive(request.IsArchived);
        await _taskLists.UpdateAsync(found, cancellationToken);
        if (request.IsArchived)
        {
            await LetTheGroupsGoAsync(request, cancellationToken);
        }

        return true;
    }

    /// <summary>
    /// Takes the list out of every group gathering it. Asked for on 2026-09-18 and confirmed for lists
    /// the same day: something put away should come off what it was attached to, the way a shelf put
    /// away comes off the lists measured against it (ArchiveInventoryCommandHandler).
    ///
    /// A group goes on standing for what it gathers, so one holding a list its owner had filed out of
    /// sight kept counting that list's work into its own progress, and the reader could not see why -
    /// the row is a pointer, and the thing it pointed at was in the Archived tab.
    ///
    /// Only on the way in. Bringing a list back does not put it back into the groups that held it,
    /// because nothing records which they were: gathering is somebody adding a row to a group, and
    /// guessing would be writing a row they did not write. See TaskList.StopGathering for what happens
    /// to the row itself.
    ///
    /// Private lists are passed over: the server holds no readable entries for one, so it cannot say
    /// whether it gathers anything - the same reason ShelfUsage leaves them out of its count.
    /// </summary>
    private async Task LetTheGroupsGoAsync(ArchiveTaskListCommand request, CancellationToken cancellationToken)
    {
        var candidates = (await _taskLists.GetAllAsync(request.UserId, updatedSinceUtc: null, cancellationToken))
            .Where(taskList => taskList.Id != request.TaskListId && !taskList.IsPrivate)
            .ToList();

        // What those lists asked of the shelves before this, taken while the rows are still there: a row
        // that stood for a list could also have stood for a shelf item, and dropping it lowers what that
        // item is wanted for - see ShelfUsage.
        var shelfItemsBefore = ShelfUsage.ShelfItemsOf(candidates);

        var gathering = candidates.Where(taskList => taskList.StopGathering(request.TaskListId)).ToList();
        if (gathering.Count == 0)
        {
            return;
        }

        await _taskLists.UpdateManyAsync(gathering, cancellationToken);
        if (_shelfUsage is not null)
        {
            await _shelfUsage.RecountAsync(request.UserId, shelfItemsBefore, cancellationToken);
        }
    }
}
