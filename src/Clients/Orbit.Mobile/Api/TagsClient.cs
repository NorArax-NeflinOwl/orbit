using System.Net;
using System.Net.Http.Json;
using Orbit.Contracts.Tags;

namespace Orbit.Mobile.Api;

/// <summary>
/// The account's tag colours on the server - see Orbit.Core.Tags.TagColour. Only TagColourSynchronizer
/// calls this; screens read the colours this phone holds (LocalTagColourRepository), as they read
/// everything else. The tags themselves travel on the notes and lists that carry them.
/// </summary>
public sealed class TagsClient
{
    private readonly HttpClient _httpClient;

    public TagsClient(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<IReadOnlyList<TagColourDto>> GetColoursAsync(CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<IReadOnlyList<TagColourDto>>("api/tags/colours", cancellationToken) ?? [];

    /// <summary>Gives a tag a colour for the whole account, or takes it away with an empty one.</summary>
    public async Task<WriteOutcome> SetColourAsync(string tag, string colour, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PutAsJsonAsync(
            "api/tags/colours", new SetTagColourRequest(tag, colour), cancellationToken);
        return response.StatusCode switch
        {
            HttpStatusCode.BadRequest => WriteOutcome.Rejected,
            HttpStatusCode.Conflict or HttpStatusCode.Forbidden => WriteOutcome.Refused,
            _ => response.IsSuccessStatusCode
                ? WriteOutcome.Applied
                : throw new HttpRequestException(
                    $"The server answered {(int)response.StatusCode} to a tag colour.", null, response.StatusCode)
        };
    }
}
