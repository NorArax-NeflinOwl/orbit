using System.Net;
using System.Net.Http.Json;
using Orbit.Core.Sync;

namespace Orbit.Mobile.Api;

/// <summary>
/// Taking up a share somebody offered, and finding out whether it already has been.
///
/// One client across all four kinds rather than a pair of methods on each area's client: the two calls
/// differ in nothing but a path segment, and the screen that makes them is a conversation, which does
/// not otherwise deal in notes or warehouses at all.
/// </summary>
public sealed class SharesClient
{
    /// <summary>
    /// Where each kind's shares live. Keyed by <see cref="SyncEntityType"/> so an offer unwrapped from a
    /// message can be handed straight over - see Orbit.Mobile.Chat.ShareOffer.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> AreasByEntityType = new Dictionary<string, string>
    {
        [SyncEntityType.Note] = "notes",
        [SyncEntityType.TaskList] = "tasks",
        [SyncEntityType.CalendarEvent] = "calendar-events",
        [SyncEntityType.Warehouse] = "warehouses"
    };

    private readonly HttpClient _httpClient;

    public SharesClient(HttpClient httpClient) => _httpClient = httpClient;

    /// <summary>
    /// Whether the offer has already been taken up, or null when the server does not know it - which is
    /// what a share meant for somebody else, or one whose subject has since been deleted, looks like.
    /// Asked rather than remembered, so an offer accepted on another device does not still read as
    /// waiting here.
    /// </summary>
    public async Task<bool?> IsAcceptedAsync(
        string entityType, Guid shareId, CancellationToken cancellationToken = default)
    {
        if (!AreasByEntityType.TryGetValue(entityType, out var area))
        {
            return null;
        }

        var response = await _httpClient.GetAsync($"api/{area}/shares/{shareId}/status", cancellationToken);
        if (response.StatusCode is HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<bool>(cancellationToken);
    }

    /// <summary>
    /// Takes the offer up, answering false when the server will not have it. Accepting twice is not an
    /// error - the second answers the same as the first - so a tap that arrives while the app is still
    /// showing the offer costs nothing.
    /// </summary>
    public async Task<bool> AcceptAsync(
        string entityType, Guid shareId, CancellationToken cancellationToken = default)
    {
        if (!AreasByEntityType.TryGetValue(entityType, out var area))
        {
            return false;
        }

        var response = await _httpClient.PostAsync($"api/{area}/shares/{shareId}/accept", content: null, cancellationToken);
        if (response.StatusCode is HttpStatusCode.NotFound)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();
        return true;
    }
}
