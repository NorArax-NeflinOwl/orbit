using Microsoft.JSInterop;

namespace Orbit.Web.Services;

/// <summary>
/// The people and groups this reader keeps at the top of their lists - see <see cref="DevicePins"/> for
/// why the answer lives on the device rather than on the server.
/// </summary>
public sealed class ConversationPins : DevicePins
{
    public ConversationPins(IJSRuntime jsRuntime)
        : base(jsRuntime, "orbit-conversation-pins")
    {
    }
}
