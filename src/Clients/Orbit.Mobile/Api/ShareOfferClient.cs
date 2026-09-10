using System.Net;
using System.Net.Http.Json;
using Orbit.Contracts.Sharing;

namespace Orbit.Mobile.Api;

/// <summary>
/// One offer, read by the person it was made to: what was offered, what it is called, and whether they
/// have already taken it up. What the invitation screen shows before anybody presses anything - see
/// InvitationViewModel, and Orbit.Api's ShareOfferEndpoints for why one address answers for every kind.
///
/// Accepting is not here. That stays where it already was, on each section's own endpoint, because the
/// rules for accepting differ by kind - see SharedItemAcceptance, which is the phone's one place for it.
/// </summary>
public sealed class ShareOfferClient
{
    private readonly HttpClient _httpClient;

    public ShareOfferClient(HttpClient httpClient) => _httpClient = httpClient;

    /// <summary>
    /// The offer, or null when there is none to read: it was withdrawn, it was never made to this
    /// account, or the kind is one the server does not know. The server answers all three the same way
    /// on purpose - an offer made to somebody else must not be distinguishable from one that never
    /// existed - and so does this.
    /// </summary>
    /// <param name="kindPath">
    /// Which kind of thing, by the name it carries inside an address - see InvitationOffer.KindPath.
    /// </param>
    public async Task<ShareOfferDto?> ReadAsync(
        string kindPath, Guid shareId, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(
            $"api/shares/{kindPath}/{shareId}", cancellationToken);

        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ShareOfferDto>(cancellationToken);
    }
}
