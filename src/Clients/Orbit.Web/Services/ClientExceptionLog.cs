using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Orbit.Web.Services;

/// <summary>Mirrors PersistentLoggerProvider's shape as mirrored into localStorage by clientLogging.js's appendLog - see window.OrbitClientLogging.getEntries.</summary>
public sealed record ClientLogEntry(
    [property: JsonPropertyName("t")] DateTimeOffset TimestampUtc,
    [property: JsonPropertyName("lvl")] string Level,
    [property: JsonPropertyName("cat")] string Category,
    [property: JsonPropertyName("msg")] string Message,
    [property: JsonPropertyName("ex")] string? Exception);

/// <summary>
/// This browser's own captured client-side errors, kept in localStorage by clientLogging.js and shown
/// alongside the server feed in the notifications panel. Purely per-device debug info that never leaves
/// the browser, unlike the push/chat entries - see the Notifications section of info/functionality.md.
/// Lives here rather than inline in a page because both the desktop popup and the mobile page show it.
/// </summary>
public sealed class ClientExceptionLog
{
    /// <summary>Enough to cover a burst of related failures without turning the panel into a log viewer.</summary>
    private const int MaxEntries = 20;

    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<ClientExceptionLog> _logger;

    public ClientExceptionLog(IJSRuntime jsRuntime, ILogger<ClientExceptionLog> logger)
    {
        _jsRuntime = jsRuntime;
        _logger = logger;
    }

    /// <summary>The most recent Error-level entries, newest first.</summary>
    public async Task<IReadOnlyList<ClientLogEntry>> GetRecentErrorsAsync()
    {
        var entries = await _jsRuntime.InvokeAsync<List<ClientLogEntry>>("OrbitClientLogging.getEntries");
        return entries
            .Where(entry => string.Equals(entry.Level, "Error", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(entry => entry.TimestampUtc)
            .Take(MaxEntries)
            .ToList();
    }

    public async Task ClearAsync() => await _jsRuntime.InvokeVoidAsync("OrbitClientLogging.clearEntries");

    /// <summary>
    /// A denied clipboard permission (some mobile browsers, restricted webviews) throws a JSException -
    /// this is the whole point of the copy action (giving a person without devtools access a way to get
    /// error details out), so a denial must degrade to nothing happening rather than crashing the caller.
    /// </summary>
    public async Task CopyToClipboardAsync(ClientLogEntry entry)
    {
        var text = $"[{entry.TimestampUtc:O}] {entry.Level} {entry.Category}: {entry.Message}"
            + (entry.Exception is null ? "" : $"\n{entry.Exception}");
        try
        {
            await _jsRuntime.InvokeVoidAsync("navigator.clipboard.writeText", text);
        }
        catch (JSException exception)
        {
            _logger.LogWarning(exception, "Failed to copy exception details to the clipboard");
        }
    }
}
