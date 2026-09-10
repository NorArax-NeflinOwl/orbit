using Microsoft.JSInterop;

namespace Orbit.Web.Services;

/// <summary>
/// When this browser was last interrupted by the advert that covers the page - the one fact
/// <see cref="AdInterruption"/> needs to leave somebody alone for a while.
///
/// Kept on the device rather than on the account, like MapPinVisibility and the panel preferences: it
/// is about one person looking at one screen, and a second browser has interrupted nobody. Held here
/// rather than in a field on the layout because a field lasts as long as the page does, and a page
/// lasts until the next refresh - which is exactly the interval that was being complained about.
///
/// A browser that cannot be written to (storage declined, a private window) reads back nothing, which
/// means "nobody has been interrupted yet" and puts the advert up again. That is the honest answer for
/// a device that has not been allowed to remember: the alternative is a gate that guesses.
/// </summary>
public sealed class LastAdInterruption
{
    private const string StorageKey = "orbit-last-advert";

    private readonly IJSRuntime _jsRuntime;

    public LastAdInterruption(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    /// <summary>
    /// When the last one went up, or null where none has - a browser that has never shown one, and one
    /// that cannot remember, give the same answer and are meant to.
    /// </summary>
    public async Task<DateTimeOffset?> ReadAsync()
    {
        try
        {
            var stored = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", StorageKey);
            // A stored value nobody can read is treated as none rather than as now: the failure that
            // shows one advert too many is better than the one that shows none ever again.
            return DateTimeOffset.TryParse(stored, out var lastShown) ? lastShown : null;
        }
        catch (JSException)
        {
            return null;
        }
    }

    /// <summary>
    /// Notes that one has just gone up. Round-trip safe: written as a round-trip format so the parse
    /// above reads back the same instant whatever the reader's language is set to.
    /// </summary>
    public async Task RecordShownAsync(DateTimeOffset shownAtUtc)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", StorageKey, shownAtUtc.ToString("O"));
        }
        catch (JSException)
        {
            // The gap still holds for as long as this page lasts - see MainLayout, which keeps the
            // answer it read. It simply will not survive the next refresh.
        }
    }
}
