using System.Net;
using System.Net.Http.Json;
using Orbit.Contracts.Config;
using Orbit.Contracts.Sharing;

namespace Orbit.Mobile.Api;

/// <summary>
/// Links that let somebody read one thing without an Orbit account at all. A different kind of sharing
/// from offering a copy to another account: nobody accepts it, and whoever holds the link can read.
///
/// The link points at a page in the browser client rather than at the API, and the phone has no way of
/// knowing that address on its own - the browser reads it off its own origin. So it asks the server,
/// which knows it from the same setting CORS is built from - see ClientFlagsDto.WebAddress.
/// </summary>
public sealed class PublicShareClient
{
    private readonly HttpClient _httpClient;

    public PublicShareClient(HttpClient httpClient) => _httpClient = httpClient;

    /// <summary>The link this item already has, or null when it has none. Asking does not make one.</summary>
    public async Task<string?> FindLinkAsync(
        string itemType, Guid itemId, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(
            $"api/share-links/{itemType}/{itemId}", cancellationToken);

        if (response.StatusCode is HttpStatusCode.NoContent || !response.IsSuccessStatusCode)
        {
            return null;
        }

        return (await response.Content.ReadFromJsonAsync<PublicShareLinkDto>(cancellationToken))?.Token;
    }

    public async Task<string?> CreateLinkAsync(
        string itemType, Guid itemId, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            "api/share-links", new CreatePublicShareLinkRequest(itemType, itemId), cancellationToken);

        return response.IsSuccessStatusCode
            ? (await response.Content.ReadFromJsonAsync<PublicShareLinkDto>(cancellationToken))?.Token
            : null;
    }

    /// <summary>Withdraws the link. Whoever already opened it keeps nothing - the page stops answering.</summary>
    public async Task RevokeLinkAsync(string itemType, Guid itemId, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.DeleteAsync(
            $"api/share-links/{itemType}/{itemId}", cancellationToken);

        _ = response;
    }

    /// <summary>
    /// Where the browser client lives, so a link can be built around a token. Empty when the deployment
    /// has not said, in which case there is no link worth offering.
    /// </summary>
    public async Task<string> WebAddressAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var flags = await _httpClient.GetFromJsonAsync<ClientFlagsDto>(
                "api/config/client-flags", cancellationToken);

            return flags?.WebAddress ?? string.Empty;
        }
        catch (Exception exception) when (exception is HttpRequestException or NotSupportedException)
        {
            return string.Empty;
        }
    }
}
