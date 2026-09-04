using System.Net.Http.Json;
using Orbit.Contracts.Suggestions;
using Orbit.Core.Suggestions;

namespace Orbit.Web.Services;

/// <summary>
/// Names the reader has already used, for the fields where the same thing gets typed twenty ways.
/// Nothing here reaches a language model - see Orbit.Core.Suggestions.GetNameSuggestions for why this
/// is a database question.
/// </summary>
public sealed class NameSuggestionsApiClient
{
    private readonly HttpClient _httpClient;

    public NameSuggestionsApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Empty rather than throwing when the lookup fails: a text field that stops accepting typing
    /// because a suggestion could not be fetched is worse than one that suggests nothing.
    /// </summary>
    public async Task<IReadOnlyList<NameSuggestionDto>> FindAsync(
        NameSuggestionKind kind, string typed, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<List<NameSuggestionDto>>(
                $"api/suggestions/names?kind={kind}&query={Uri.EscapeDataString(typed)}", cancellationToken) ?? [];
        }
        catch (HttpRequestException)
        {
            return [];
        }
        catch (TaskCanceledException)
        {
            // The reader typed another character before this one came back, which is the ordinary case.
            return [];
        }
    }

    /// <summary>
    /// Everything the reader has already filed something under for that field - see UsedValueKind for
    /// why this is a different question from the one above. Read once when an editor opens, so unlike
    /// FindAsync there is nothing to narrow it by and nothing racing another keystroke.
    ///
    /// Empty rather than throwing, for the same reason: a category box that will not accept typing
    /// because its list could not be fetched is worse than one with no list.
    /// </summary>
    public async Task<IReadOnlyList<string>> UsedValuesAsync(
        UsedValueKind kind, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<List<string>>(
                $"api/suggestions/used-values?kind={kind}", cancellationToken) ?? [];
        }
        catch (HttpRequestException)
        {
            return [];
        }
        catch (TaskCanceledException)
        {
            return [];
        }
    }
}
