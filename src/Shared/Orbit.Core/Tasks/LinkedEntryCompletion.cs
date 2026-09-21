namespace Orbit.Core.Tasks;

/// <summary>
/// One entry a reminder query found that points at other lists - as a list it stands for, or as a way
/// of doing it - together with whose lists have to be asked about it.
/// </summary>
public sealed record EntryPointingAtLists(Guid UserId, Guid TaskItemId);

/// <summary>
/// Whether the lists an entry points at have finished it. The one question neither reminder query can
/// ask in SQL: a linked entry's stored tick is always "not done" (see <see cref="TaskItem.Create"/>),
/// and a way that is a list is worked out on every read rather than stored (see
/// <see cref="LinkedTaskCompletionResolver"/>), so the entry's own row says nothing about whether the
/// work is behind it.
///
/// Both queries used to answer it by leaving every such entry out, which meant an entry standing for
/// another list said nothing at all when its deadline passed - reported by the user on 2026-09-21, with
/// an entry due at 17:00 that never spoke. Leaving them in and asking the lists is the same rule read
/// the other way round, and it is the reading that keeps a deadline somebody set worth setting.
///
/// Asked the way every read of a list asks it - the resolver over everything the owner has - rather
/// than with a query of its own, so a reminder and the checklist cannot come to different answers about
/// the same entry. That is one read per owner per poll, and only for an owner who has such an entry
/// waiting on a reminder; every other poll asks nothing extra.
/// </summary>
public sealed class LinkedEntryCompletion
{
    private readonly ITaskRepository _taskRepository;
    private readonly LinkedTaskCompletionResolver _linkedTaskCompletionResolver;

    public LinkedEntryCompletion(ITaskRepository taskRepository, LinkedTaskCompletionResolver linkedTaskCompletionResolver)
    {
        _taskRepository = taskRepository;
        _linkedTaskCompletionResolver = linkedTaskCompletionResolver;
    }

    /// <summary>
    /// Which of <paramref name="entries"/> the lists behind them have finished - ticked off or given up
    /// on, since the work is no longer owed either way (see <see cref="TaskItem.IsResolved"/>). An entry
    /// whose lists cannot be resolved is not among them: an unresolvable link counts as unfinished here
    /// exactly as it does everywhere else.
    /// </summary>
    public async Task<IReadOnlySet<Guid>> FinishedByTheirListsAsync(
        IReadOnlyCollection<EntryPointingAtLists> entries, CancellationToken cancellationToken)
    {
        var finished = new HashSet<Guid>();
        foreach (var owner in entries.GroupBy(entry => entry.UserId))
        {
            var asked = owner.Select(entry => entry.TaskItemId).ToHashSet();
            var taskLists = await _taskRepository.GetAllAsync(owner.Key, null, cancellationToken);
            finished.UnionWith(_linkedTaskCompletionResolver.ResolveAll(taskLists)
                .SelectMany(taskList => taskList.Items)
                .Where(item => asked.Contains(item.Id) && item.IsResolved)
                .Select(item => item.Id));
        }

        return finished;
    }
}
