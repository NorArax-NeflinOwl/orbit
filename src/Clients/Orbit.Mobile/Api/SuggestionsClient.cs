using System.Net.Http.Json;
using Orbit.Contracts.Suggestions;
using Orbit.Core.Suggestions;

namespace Orbit.Mobile.Api;

/// <summary>
/// Names the reader has already used, for the fields where the same thing gets typed twenty ways. The
/// counterpart of Orbit.Web's NameSuggestionsApiClient, against the same endpoint.
///
/// Nothing here reaches a language model - see Orbit.Core.Suggestions.GetNameSuggestions for why this
/// is a database question, and a better-answered one.
/// </summary>
public sealed class SuggestionsClient
{
    private readonly HttpClient _httpClient;

    public SuggestionsClient(HttpClient httpClient) => _httpClient = httpClient;

    /// <summary>
    /// Empty rather than throwing when the lookup fails, which on a phone is an ordinary state rather
    /// than an error: a field that stopped accepting typing because a suggestion could not be fetched
    /// would be worse than one that suggests nothing.
    /// </summary>
    public async Task<IReadOnlyList<NameSuggestionDto>> FindAsync(
        NameSuggestionKind kind, string typed, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<List<NameSuggestionDto>>(
                $"api/suggestions/names?kind={kind}&query={Uri.EscapeDataString(typed)}", cancellationToken) ?? [];
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            // Offline, or the reader typed another character before this one came back - the ordinary case.
            return [];
        }
    }
}
