using Microsoft.JSInterop;

namespace Orbit.Web.Services;

/// <summary>
/// Whether the calendar's two side panels - the list of events and the list of things due - are open.
///
/// Kept on the device, like the Tasks page's arrangement and the dashboard's own layout: it is how one
/// person reads one page on one screen. Both used to reset on every visit, so somebody who works with
/// the event list open had to open it again every single time they came back to the calendar.
/// </summary>
public sealed class CalendarPanelPreference
{
    private const string EventListKey = "orbit-calendar-event-list";
    private const string TaskListKey = "orbit-calendar-task-list";

    private readonly IJSRuntime _jsRuntime;

    public CalendarPanelPreference(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    /// <summary>Both closed until somebody opens them: the grid is the page, and the panels are opt-in.</summary>
    public bool IsEventListVisible { get; private set; }

    public bool IsTaskListVisible { get; private set; }

    public async Task InitializeAsync()
    {
        IsEventListVisible = await ReadAsync(EventListKey) == "true";
        IsTaskListVisible = await ReadAsync(TaskListKey) == "true";
    }

    public Task SetEventListVisibleAsync(bool isVisible)
    {
        IsEventListVisible = isVisible;
        return WriteAsync(EventListKey, isVisible);
    }

    public Task SetTaskListVisibleAsync(bool isVisible)
    {
        IsTaskListVisible = isVisible;
        return WriteAsync(TaskListKey, isVisible);
    }

    /// <summary>
    /// Mirrors DevicePreferences: a browser with storage blocked outright (private windows, embedded
    /// webviews) throws here, and the right answer then is the default - both panels closed.
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

    private async Task WriteAsync(string key, bool isVisible)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", key, isVisible ? "true" : "false");
        }
        catch (JSException)
        {
            // It still applies for this session - it just won't be remembered for the next one.
        }
    }
}
