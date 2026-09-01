using Microsoft.JSInterop;

namespace Orbit.Web.Services;

/// <summary>The orders the calendar's list beside the grid can be read in.</summary>
public enum CalendarListSortOrder
{
    /// <summary>
    /// When each thing happens, soonest first. The default, and the one the list exists for: the
    /// question a calendar answers is what is coming.
    /// </summary>
    When,

    /// <summary>
    /// Events first, then deadlines - and within each, still by when. For a reader who came looking
    /// for one kind of thing in a period that holds a lot of both.
    /// </summary>
    Type,

    /// <summary>By name, for finding one thing whose title is what the reader remembers about it.</summary>
    Alphabetical
}

/// <summary>
/// How this reader wants the calendar's list ordered. Kept on the device rather than on the account,
/// like <see cref="TaskListArrangement"/> and <see cref="PanelPreferences"/>: it describes one page for
/// one reader on one screen and says nothing about what is on it.
/// </summary>
public sealed class CalendarListOrder
{
    private const string SortOrderKey = "orbit-calendar-list-sort-order";

    private readonly IJSRuntime _jsRuntime;

    public CalendarListOrder(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public CalendarListSortOrder SortOrder { get; private set; } = CalendarListSortOrder.When;

    /// <summary>
    /// Reads what was stored. Anything unreadable - a browser with storage blocked, a value written by
    /// a build that offered a different order - leaves the default standing.
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            if (Enum.TryParse<CalendarListSortOrder>(
                    await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", SortOrderKey), out var stored))
            {
                SortOrder = stored;
            }
        }
        catch (JSException)
        {
            // The default is the right answer for a browser that cannot tell us otherwise.
        }
    }

    public async Task SetSortOrderAsync(CalendarListSortOrder sortOrder)
    {
        SortOrder = sortOrder;
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", SortOrderKey, sortOrder.ToString());
        }
        catch (JSException)
        {
            // It still applies for this session - it just won't be remembered for the next one.
        }
    }
}
