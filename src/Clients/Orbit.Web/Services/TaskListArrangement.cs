using System.Text.Json;
using Microsoft.JSInterop;

namespace Orbit.Web.Services;

/// <summary>The orders the Tasks page can put its cards in.</summary>
public enum TaskListSortOrder
{
    /// <summary>How much each list matters, most first - see ItemPriority.</summary>
    Priority,

    /// <summary>The same, upside down: the small things, for a reader clearing them out of the way.</summary>
    LeastImportantFirst,
    Newest,
    Oldest,
    Alphabetical,
    ReverseAlphabetical,

    /// <summary>Wherever the reader dragged each card - see <see cref="TaskListArrangement.ManualOrder"/>.</summary>
    Manual
}

/// <summary>
/// How much of each list the Tasks page shows. One answer for the whole page, because the question is
/// "how am I reading this today" rather than one about any particular list.
/// </summary>
public enum TaskListView
{
    /// <summary>
    /// Every card folded to its heading. For a page of lists somebody is scanning rather than working
    /// from - and it does not touch which cards were folded by hand, so leaving this view puts those
    /// back exactly as they were.
    /// </summary>
    Minimal,

    /// <summary>The default: enough of each list to recognise it, and no more.</summary>
    Normal,

    /// <summary>
    /// As much of each list as a card can carry, for a page somebody is working down rather than
    /// scanning. Still bounded - see Tasks.razor's PreviewLimitFor, which is where the numbers live,
    /// because how much fits on a card is a question about the card rather than about the reader.
    /// </summary>
    Full
}

/// <summary>
/// How this reader arranges the Tasks page: what to sort the cards by, and - when that answer is "the
/// way I put them" - the order they dragged them into.
///
/// The two travel together because one is meaningless without the other: a dragged order nobody is
/// sorting by is invisible, and choosing to sort by it with nothing stored is choosing nothing.
///
/// Kept on the device rather than on the account, like <see cref="DashboardCardPreferences"/> and
/// <see cref="ChecklistViewPreference"/>: this describes one page for one reader and says nothing about
/// the lists themselves.
/// </summary>
public sealed class TaskListArrangement
{
    private const string SortOrderKey = "orbit-task-list-sort-order";
    private const string ManualOrderKey = "orbit-task-list-manual-order";
    private const string CollapsedKey = "orbit-task-list-collapsed";
    private const string ViewKey = "orbit-task-list-view";
    private const string ViewBeforeMinimalKey = "orbit-task-list-view-before-minimal";

    private readonly IJSRuntime _jsRuntime;

    public TaskListArrangement(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public TaskListSortOrder SortOrder { get; private set; } = TaskListSortOrder.Priority;

    /// <summary>How much of each list is on show. Normal until somebody says otherwise.</summary>
    public TaskListView View { get; private set; } = TaskListView.Normal;

    /// <summary>
    /// What the page looked like before it was folded away, so unfolding it goes back to where the
    /// reader was rather than to a default they may not have chosen in months. Remembered across
    /// launches for the same reason the view itself is: coming back to a folded page and expanding a
    /// card should land somewhere familiar.
    /// </summary>
    private TaskListView _viewBeforeMinimal = TaskListView.Normal;

    /// <summary>
    /// The ids the reader has dragged into place, first to last. Lists that are not on it - anything
    /// created or shared since the last drag - are not in the wrong place, they are simply not placed
    /// yet, and the page puts them after the ones that are.
    /// </summary>
    public IReadOnlyList<Guid> ManualOrder { get; private set; } = [];

    /// <summary>
    /// The cards folded down to a heading, a line of what is on them and their buttons. Collapsed rather
    /// than hidden: a list somebody is not working on this week is still a list they want to see is
    /// there, which is why this is not the same as filtering it away.
    /// </summary>
    private HashSet<Guid> _collapsed = [];

    /// <summary>
    /// Whether this card is folded - by the view, or by hand. The minimal view folds everything without
    /// writing anything down, which is what lets leaving it restore exactly the cards that were folded
    /// before.
    /// </summary>
    public bool IsCollapsed(Guid taskListId) => View == TaskListView.Minimal || _collapsed.Contains(taskListId);

    public async Task InitializeAsync()
    {
        SortOrder = Enum.TryParse<TaskListSortOrder>(await ReadAsync(SortOrderKey), out var sortOrder)
            ? sortOrder
            : TaskListSortOrder.Priority;
        ManualOrder = Read(await ReadAsync(ManualOrderKey));
        _collapsed = [.. Read(await ReadAsync(CollapsedKey))];
        View = Enum.TryParse<TaskListView>(await ReadAsync(ViewKey), out var view) ? view : TaskListView.Normal;
        _viewBeforeMinimal = Enum.TryParse<TaskListView>(await ReadAsync(ViewBeforeMinimalKey), out var before)
            && before != TaskListView.Minimal
                ? before
                : TaskListView.Normal;
    }

    /// <summary>
    /// Folding the page away remembers what it was, so unfolding it can go back. Choosing the view it is
    /// already on writes nothing new down - otherwise picking Minimal twice would make "before minimal"
    /// mean minimal, and unfolding would fold.
    /// </summary>
    public async Task SetViewAsync(TaskListView view)
    {
        if (view == TaskListView.Minimal && View != TaskListView.Minimal)
        {
            _viewBeforeMinimal = View;
            await WriteAsync(ViewBeforeMinimalKey, View.ToString());
        }

        View = view;
        await WriteAsync(ViewKey, view.ToString());
    }

    /// <summary>
    /// What expanding a card does while the whole page is folded away: it is a request to see things
    /// again, and answering it by unfolding one card would leave the page in a state the view says it is
    /// not in.
    /// </summary>
    public Task LeaveMinimalViewAsync() => SetViewAsync(_viewBeforeMinimal);

    public Task SetCollapsedAsync(Guid taskListId, bool isCollapsed)
    {
        if (isCollapsed)
        {
            _collapsed.Add(taskListId);
        }
        else
        {
            _collapsed.Remove(taskListId);
        }

        return WriteAsync(CollapsedKey, JsonSerializer.Serialize(_collapsed));
    }

    public Task SetSortOrderAsync(TaskListSortOrder sortOrder)
    {
        SortOrder = sortOrder;
        return WriteAsync(SortOrderKey, sortOrder.ToString());
    }

    public Task SetManualOrderAsync(IReadOnlyList<Guid> orderedTaskListIds)
    {
        ManualOrder = orderedTaskListIds;
        return WriteAsync(ManualOrderKey, JsonSerializer.Serialize(orderedTaskListIds));
    }

    /// <summary>An unreadable or absent value means nothing has been dragged yet, which is a fine answer.</summary>
    private static IReadOnlyList<Guid> Read(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<Guid>>(stored) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>
    /// Mirrors DevicePreferences: a browser with storage blocked outright (private windows, embedded
    /// webviews) throws here, and the right answer then is the default.
    /// </summary>
    private async Task<string?> ReadAsync(string key)
    {
        try
        {
            return await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", key);
        }
        catch (JSException)
        {
            return null;
        }
    }

    private async Task WriteAsync(string key, string value)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", key, value);
        }
        catch (JSException)
        {
            // It still applies for this session - it just won't be remembered for the next one.
        }
    }
}
