using Microsoft.JSInterop;

namespace Orbit.Web.Services;

/// <summary>
/// Whether this tab is actually in front of somebody.
///
/// Chat polls for new messages while a conversation is open, which is the right thing to do while
/// somebody is reading it and pure waste behind thirty other tabs - the same reasoning
/// <see cref="PresenceService"/> already applies to its heartbeat, which is why both ask the same
/// question of the same script (wwwroot/js/presence.js).
///
/// A tab that cannot be asked counts as visible: a poll that stops because the answer could not be
/// obtained is a chat that silently goes quiet, and quiet is indistinguishable from nobody writing.
/// </summary>
public sealed class PageVisibility
{
    private readonly IJSRuntime _jsRuntime;

    public PageVisibility(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task<bool> IsPageVisibleAsync()
    {
        try
        {
            await using var module = await _jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/presence.js");
            return await module.InvokeAsync<bool>("isPageVisible");
        }
        catch (JSException)
        {
            return true;
        }
        catch (JSDisconnectedException)
        {
            // The page is going away; whatever the caller was about to poll for, nobody is there to read.
            return false;
        }
    }
}
