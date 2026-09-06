using Microsoft.JSInterop;

namespace Orbit.Web.Services;

/// <summary>
/// The task lists and notes somebody else shared that this reader keeps at the top of their own page.
///
/// A thing you own carries its own pin, on the server, because it is yours to arrange (see
/// SetTaskListPinnedCommandHandler, which refuses anybody else). A thing shared with you cannot use that
/// pin: it belongs to whoever owns the list, and a recipient setting it would be rearranging somebody
/// else's page. So the reader's answer is kept here instead - the same choice the pinned-conversations
/// list already makes, and for the same reason. See <see cref="DevicePins"/>.
///
/// One set for both kinds. Ids are unique across them, and a reader pinning "the shopping list" and "the
/// recipe note" is doing one thing twice rather than two different things.
/// </summary>
public sealed class SharedItemPins : DevicePins
{
    public SharedItemPins(IJSRuntime jsRuntime)
        : base(jsRuntime, "orbit-shared-item-pins")
    {
    }
}
