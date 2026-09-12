using System.Net.Http.Json;
using System.Text.Json;
using Orbit.Contracts.Tags;

namespace Orbit.Web.Services;

/// <summary>
/// The colours this account gives its tags - see Orbit.Core.Tags.TagColour. The tags themselves travel on
/// the notes and task lists carrying them; see TagColourBook for how the colours are held on a page.
/// </summary>
public sealed class TagsApiClient
{
    private readonly HttpClient _httpClient;

    public TagsApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Empty rather than throwing: a card drawn plain because its colours could not be read is still a
    /// card, and a page that failed to open over a colour would be a page lost for nothing.
    /// </summary>
    public async Task<IReadOnlyList<TagColourDto>> GetColoursAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<List<TagColourDto>>("api/tags/colours", cancellationToken) ?? [];
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException
            or JsonException or NotSupportedException)
        {
            return [];
        }
    }

    /// <summary>
    /// Gives <paramref name="tag"/> this colour for the whole account, or takes it away with an empty one,
    /// and answers every colour afterwards. Throws when it fails: a colour somebody chose that did not save
    /// has to be said, not swallowed.
    /// </summary>
    public async Task<IReadOnlyList<TagColourDto>> SetColourAsync(
        string tag, string colour, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync(
            "api/tags/colours", new SetTagColourRequest(tag, colour), cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<TagColourDto>>(cancellationToken) ?? [];
    }
}
