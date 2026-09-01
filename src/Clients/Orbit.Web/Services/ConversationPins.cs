using System.Text.Json;
using Microsoft.JSInterop;

namespace Orbit.Web.Services;

/// <summary>
/// The people and groups this reader keeps at the top of their lists.
///
/// Kept on the device, like <see cref="WarehouseArrangement"/> and the dashboard's own pins: pinning
/// says which conversations matter to one person reading one screen, and the other party has their own
/// answer. It is also why this needs no column anywhere - a preference about reading is not something
/// the server has to know.
/// </summary>
public sealed class ConversationPins
{
    private const string StorageKey = "orbit-conversation-pins";

    private readonly IJSRuntime _jsRuntime;
    private HashSet<Guid> _pinned = [];

    public ConversationPins(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public bool IsPinned(Guid id) => _pinned.Contains(id);

    public async Task InitializeAsync() => _pinned = [.. Read(await ReadAsync())];

    public Task SetPinnedAsync(Guid id, bool isPinned)
    {
        if (isPinned)
        {
            _pinned.Add(id);
        }
        else
        {
            _pinned.Remove(id);
        }

        return WriteAsync(JsonSerializer.Serialize(_pinned));
    }

    /// <summary>
    /// Pinned rows first, and everything else in whatever order it arrived in - which the caller has
    /// already decided, alphabetically for people and by when something last happened for chats. No
    /// separator between the two: a pinned row is still one of the list, just at the top of it.
    /// </summary>
    public IEnumerable<T> PinnedFirst<T>(IEnumerable<T> rows, Func<T, Guid> idOf)
        => rows.OrderByDescending(row => IsPinned(idOf(row)));

    private static IReadOnlyList<Guid> Read(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<Guid>>(stored) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>A browser with storage blocked throws here, and the right answer then is "none pinned".</summary>
    private async Task<string?> ReadAsync()
    {
        try
        {
            return await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", StorageKey);
        }
        catch (JSException)
        {
            return null;
        }
    }

    private async Task WriteAsync(string value)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", StorageKey, value);
        }
        catch (JSException)
        {
            // It still applies for this session - it just won't be remembered for the next one.
        }
    }
}
