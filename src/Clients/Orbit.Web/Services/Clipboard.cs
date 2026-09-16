using Microsoft.JSInterop;

namespace Orbit.Web.Services;

/// <summary>
/// Puts text on the system clipboard, and answers whether it went.
///
/// A class rather than the one line it wraps, because the one line is never the whole of it: the
/// clipboard is denied outright in some mobile browsers and in a restricted webview, where the call
/// throws rather than answering. Every screen that copied something had written the same try/catch, and
/// a screen that forgot it would take the page down for a permission the reader never granted.
/// </summary>
public sealed class Clipboard
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<Clipboard> _logger;

    public Clipboard(IJSRuntime jsRuntime, ILogger<Clipboard> logger)
    {
        _jsRuntime = jsRuntime;
        _logger = logger;
    }

    /// <summary>
    /// False for a clipboard that refused, so the caller can say so in its own words and in its own
    /// place - which is the one thing each of them does differently.
    /// </summary>
    public async Task<bool> TryWriteAsync(string text)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("navigator.clipboard.writeText", text);
            return true;
        }
        catch (JSException exception)
        {
            _logger.LogWarning(exception, "The browser did not allow writing to the clipboard");
            return false;
        }
    }
}
