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
/// How this reader wants the calendar's list ordered, and whether it still shows what is over and done
/// with. Kept on the device rather than on the account, like <see cref="TaskListArrangement"/> and
/// <see cref="PanelPreferences"/>: it describes one page for one reader on one screen and says nothing
/// about what is on it.
/// </summary>
public sealed class CalendarListOrder
{
    private const string SortOrderKey = "orbit-calendar-list-sort-order";
    private const string ShowsEverythingKey = "orbit-calendar-list-shows-everything";

    private readonly IJSRuntime _jsRuntime;

    public CalendarListOrder(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public CalendarListSortOrder SortOrder { get; private set; } = CalendarListSortOrder.When;

    /// <summary>
    /// Whether the list still shows a task already ticked off and an event already over. Off by
    /// default: what a calendar is read for is what is coming, and a month's worth of finished work
    /// pushes it below the fold by the twentieth. Everything is still there to be asked for - see
    /// Calendar.razor's menu - and the grid never hides anything, since a day with something in it
    /// should say so whether or not it has been.
    /// </summary>
    public bool ShowsEverything { get; private set; }

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

            ShowsEverything =
                await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", ShowsEverythingKey) == "true";
        }
        catch (JSException)
        {
            // The default is the right answer for a browser that cannot tell us otherwise.
        }
    }

    public async Task ShowEverythingAsync(bool showsEverything)
    {
        ShowsEverything = showsEverything;
        try
        {
            await _jsRuntime.InvokeVoidAsync(
                "localStorage.setItem", ShowsEverythingKey, showsEverything ? "true" : "false");
        }
        catch (JSException)
        {
            // It still applies for this session - it just won't be remembered for the next one.
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
