using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;

namespace Orbit.Mobile.Screens.Tasks;

/// <summary>How the task lists are ordered, in the same ways Orbit.Web offers.</summary>
public enum TaskListSortOrder
{
    /// <summary>How much each list matters, most first.</summary>
    Priority,

    /// <summary>The same, upside down: the small things, for a reader clearing them out of the way.</summary>
    LeastImportantFirst,
    Newest,
    Oldest,
    Alphabetical,
    ReverseAlphabetical,

    /// <summary>Wherever the reader moved each card - see TaskListArrangement.ManualOrder.</summary>
    Manual
}

/// <summary>
/// How this reader has arranged the task lists: what to sort the cards by, and - when that answer is
/// "the way I put them" - the order they moved them into.
///
/// The two travel together because one is meaningless without the other: an order nobody is sorting by
/// is invisible, and choosing to sort by one with nothing stored is choosing nothing. Orbit.Web keeps
/// the same pair together for the same reason.
/// </summary>
public sealed record TaskListArrangement(TaskListSortOrder SortOrder, IReadOnlyList<Guid> ManualOrder)
{
    /// <summary>An order with nothing moved yet, which is every order but Manual.</summary>
    public static TaskListArrangement By(TaskListSortOrder sortOrder) => new(sortOrder, []);
}

/// <summary>
/// Remembers how this reader arranges their task lists, between launches - see IThemeStore for the same
/// shape, and Orbit.Web's TaskListArrangement, which keeps the same choice on the device for the same
/// reason: it describes one page for one reader and says nothing about the lists themselves.
///
/// Only the arrangement. What is filtered to is a narrowing somebody does for a moment - "show me the
/// overdue ones" - and bringing it back a week later would answer a question nobody asked twice. The web
/// draws the line in the same place.
/// </summary>
public interface ITaskListArrangementStore
{
    TaskListSortOrder ReadSortOrder();

    void WriteSortOrder(TaskListSortOrder sortOrder);

    /// <summary>The lists the reader has put in place, first to last - empty until they move one.</summary>
    IReadOnlyList<Guid> ReadManualOrder();

    void WriteManualOrder(IReadOnlyList<Guid> orderedLocalIds);

    /// <summary>The lists folded down to their heading - empty until the reader folds one.</summary>
    IReadOnlyList<Guid> ReadCollapsed();

    void WriteCollapsed(IReadOnlyList<Guid> collapsedLocalIds);
}

/// <summary>
/// Which task lists to show and in what order - the filtering and sorting the phone did not have, and
/// the same choices the web's task page makes, so the two do not disagree about what "Overdue" means.
///
/// A type of its own rather than four fields on the screen: the question "what should be on screen"
/// has one answer, and scattering it across the view model is where the two halves drift apart.
/// </summary>
public static class TaskListView
{
    /// <summary>The statuses worth filtering by, in the order the web lists them.</summary>
    public static IReadOnlyList<string> Statuses { get; } = ["New", "Pending", "Overdue", "Completed"];

    public static IReadOnlyList<LocalTaskList> Arrange(
        IReadOnlyList<LocalTaskList> taskLists, string? status, TaskListArrangement arrangement)
    {
        var visible = status is null
            ? taskLists
            : [.. taskLists.Where(taskList => taskList.Status == status)];

        // Pinned first whatever the order, because pinning is the reader saying "this one, above the
        // rule" - and a sort that ignored it would take the pin away in all but name. The order the
        // reader put the cards in is the exception: it already says where every card goes, so a pin
        // would contradict it. Orbit.Web draws the line in the same place.
        return arrangement.SortOrder == TaskListSortOrder.Manual
            ? [.. AsArranged(visible, arrangement.ManualOrder)]
            : [.. Sort(visible, arrangement.SortOrder).OrderByDescending(taskList => taskList.IsPinned)];
    }

    /// <summary>
    /// The reader's own order, with anything they have not placed yet - a list made or shared since they
    /// last moved one - after it rather than at the front, where it would push their arrangement about.
    /// </summary>
    private static IEnumerable<LocalTaskList> AsArranged(
        IReadOnlyList<LocalTaskList> taskLists, IReadOnlyList<Guid> manualOrder)
    {
        var placeByLocalId = manualOrder
            .Select((localId, place) => (localId, place))
            .ToDictionary(entry => entry.localId, entry => entry.place);
        return taskLists.OrderBy(taskList => placeByLocalId.GetValueOrDefault(taskList.LocalId, int.MaxValue));
    }

    public static string Describe(string status, Translations translations) => status switch
    {
        "New" => translations["New"],
        "Pending" => translations["Pending"],
        "Overdue" => translations["Overdue"],
        _ => translations["Completed"]
    };

    public static string Describe(TaskListSortOrder order, Translations translations) => order switch
    {
        TaskListSortOrder.Priority => translations["Most important first"],
        TaskListSortOrder.LeastImportantFirst => translations["Least important first"],
        TaskListSortOrder.Newest => translations["Newest first"],
        TaskListSortOrder.Oldest => translations["Oldest first"],
        TaskListSortOrder.Alphabetical => translations["A to Z"],
        TaskListSortOrder.Manual => translations["The way I arranged them"],
        _ => translations["Z to A"]
    };

    private static IEnumerable<LocalTaskList> Sort(IEnumerable<LocalTaskList> taskLists, TaskListSortOrder order)
        => order switch
        {
            TaskListSortOrder.Priority => taskLists.OrderByDescending(Rank),
            TaskListSortOrder.LeastImportantFirst => taskLists.OrderBy(Rank),
            TaskListSortOrder.Newest => taskLists.OrderByDescending(taskList => taskList.CreatedAtUtc),
            TaskListSortOrder.Oldest => taskLists.OrderBy(taskList => taskList.CreatedAtUtc),
            TaskListSortOrder.Alphabetical
                => taskLists.OrderBy(taskList => taskList.Title, StringComparer.CurrentCultureIgnoreCase),
            _ => taskLists.OrderByDescending(taskList => taskList.Title, StringComparer.CurrentCultureIgnoreCase)
        };

    /// <summary>Highest first. An unknown name sorts as Normal rather than throwing - a priority added in a later build is not a crash.</summary>
    private static int Rank(LocalTaskList taskList) => taskList.Priority switch
    {
        "Highest" => 4,
        "High" => 3,
        "Low" => 1,
        "Lowest" => 0,
        _ => 2
    };
}

/// <summary>
/// One filter chip: what it filters to, what it is called, how many it would leave, and whether it is
/// the one currently chosen. A null <paramref name="Status"/> is "all of them".
/// </summary>
public sealed record TaskListFilter(string? Status, string Name, int Count, bool IsChosen)
{
    /// <summary>What the chip says. The count is what makes it worth tapping - or worth not tapping.</summary>
    public string Label => $"{Name} {Count}";
}

/// <summary>
/// One order the lists can be put in, as the menu offers it: what it is, what it is called, and whether
/// it is the one in force - a menu of six with no answer marked leaves the reader guessing.
/// </summary>
public sealed record TaskListSortChoice(TaskListSortOrder Order, string Name, bool IsChosen);
