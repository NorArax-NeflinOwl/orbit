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

    private readonly IJSRuntime _jsRuntime;

    public TaskListArrangement(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public TaskListSortOrder SortOrder { get; private set; } = TaskListSortOrder.Priority;

    /// <summary>
    /// The ids the reader has dragged into place, first to last. Lists that are not on it - anything
    /// created or shared since the last drag - are not in the wrong place, they are simply not placed
    /// yet, and the page puts them after the ones that are.
    /// </summary>
    public IReadOnlyList<Guid> ManualOrder { get; private set; } = [];

    public async Task InitializeAsync()
    {
        SortOrder = Enum.TryParse<TaskListSortOrder>(await ReadAsync(SortOrderKey), out var sortOrder)
            ? sortOrder
            : TaskListSortOrder.Priority;
        ManualOrder = Read(await ReadAsync(ManualOrderKey));
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
