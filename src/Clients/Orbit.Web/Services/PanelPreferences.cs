using Microsoft.JSInterop;

namespace Orbit.Web.Services;

/// <summary>
/// Which of the app's side panels this reader keeps open: the calendar's list of events and list of
/// things due, and the conversation list beside a chat.
///
/// One class rather than one per page, because it is one question asked in three places and the answer
/// is stored the same way each time. Kept on the device, like the Tasks page's arrangement and the
/// dashboard's own layout: it is how one person reads one page on one screen.
///
/// Everything defaults to closed. That is what a panel nobody has opened should be, and it means a
/// browser with storage blocked (private windows, embedded webviews) behaves like a fresh one rather
/// than throwing.
/// </summary>
public sealed class PanelPreferences
{
    /// <summary>
    /// The panels there are. Named rather than passed as strings by callers, so a typo is a compiler
    /// error instead of a preference that silently never comes back.
    /// </summary>
    public enum Panel
    {
        /// <summary>The calendar's list of events beside the grid.</summary>
        CalendarEventList,

        /// <summary>The calendar's list of things due beside the grid.</summary>
        CalendarTaskList,

        /// <summary>The conversation list beside a chat, when it is showing names rather than initials.</summary>
        ChatConversationList
    }

    private readonly IJSRuntime _jsRuntime;
    private readonly Dictionary<Panel, bool> _openPanels = [];

    public PanelPreferences(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public bool IsOpen(Panel panel) => _openPanels.GetValueOrDefault(panel);

    public async Task InitializeAsync()
    {
        foreach (var panel in Enum.GetValues<Panel>())
        {
            _openPanels[panel] = await ReadAsync(panel) == "true";
        }
    }

    public async Task SetOpenAsync(Panel panel, bool isOpen)
    {
        _openPanels[panel] = isOpen;
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", StorageKey(panel), isOpen ? "true" : "false");
        }
        catch (JSException)
        {
            // It still applies for this session - it just won't be remembered for the next one.
        }
    }

    /// <summary>
    /// Mirrors DevicePreferences: a browser with storage blocked outright throws here, and the right
    /// answer then is the default.
    /// </summary>
    private async Task<string?> ReadAsync(Panel panel)
    {
        try
        {
            return await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", StorageKey(panel));
        }
        catch (JSException)
        {
            return null;
        }
    }

    private static string StorageKey(Panel panel) => $"orbit-panel-{panel}";
}
