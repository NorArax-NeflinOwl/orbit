using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Orbit.Web.Services;

/// <summary>
/// Asks the page what of a chat thread is in front of somebody (wwwroot/js/chatSeen.js). Only asks:
/// what that makes read is ChatReadState's decision.
///
/// Unlike <see cref="PageVisibility"/>, a question that cannot be asked counts as "nothing seen". There
/// the cost of a wrong answer is a chat that stops updating; here it is a read receipt for something
/// nobody saw, which is exactly what this replaced. A message left unread by a broken script is marked
/// the next time the question can be answered.
/// </summary>
public sealed class ChatSeenProbe
{
    private const string ModulePath = "./js/chatSeen.js";

    private readonly IJSRuntime _jsRuntime;

    public ChatSeenProbe(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    /// <summary>
    /// The newest message whose end is on screen in this list, provided the tab is visible and the
    /// window has focus - otherwise null, however much is on screen.
    /// </summary>
    public async Task<Guid?> NewestMessageSeenAsync(ElementReference messageList)
    {
        try
        {
            await using var module = await _jsRuntime.InvokeAsync<IJSObjectReference>("import", ModulePath);
            if (!await module.InvokeAsync<bool>("isInFront"))
            {
                return null;
            }

            var messageId = await module.InvokeAsync<string?>("newestInView", messageList);
            return Guid.TryParse(messageId, out var parsed) ? parsed : null;
        }
        catch (JSException)
        {
            return null;
        }
        catch (JSDisconnectedException)
        {
            return null;
        }
    }

    /// <summary>
    /// Has the page call <c>OnThreadSeenMayHaveChanged</c> on the component when the list scrolls, the
    /// window gets focus or the tab comes to the front - the moments a message can become seen between
    /// two polls. The key is the caller's own, and is what <see cref="UnobserveAsync"/> takes.
    /// </summary>
    public async Task ObserveAsync<TComponent>(
        string key, ElementReference messageList, DotNetObjectReference<TComponent> component)
        where TComponent : class
    {
        try
        {
            await using var module = await _jsRuntime.InvokeAsync<IJSObjectReference>("import", ModulePath);
            await module.InvokeVoidAsync("observe", key, messageList, component);
        }
        catch (JSException)
        {
            // Seen state is still checked on every poll; this only makes it prompt.
        }
        catch (JSDisconnectedException)
        {
            // The page is going away.
        }
    }

    public async Task UnobserveAsync(string key)
    {
        try
        {
            await using var module = await _jsRuntime.InvokeAsync<IJSObjectReference>("import", ModulePath);
            await module.InvokeVoidAsync("unobserve", key);
        }
        catch (JSException)
        {
            // Nothing was observed under this key.
        }
        catch (JSDisconnectedException)
        {
            // The page is going away, and its listeners with it.
        }
    }
}
