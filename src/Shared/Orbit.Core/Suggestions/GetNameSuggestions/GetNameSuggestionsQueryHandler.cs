using Orbit.Core.Abstractions;

namespace Orbit.Core.Suggestions.GetNameSuggestions;

public sealed class GetNameSuggestionsQueryHandler
    : IRequestHandler<GetNameSuggestionsQuery, IReadOnlyList<NameSuggestion>>
{
    /// <summary>
    /// Below this, two names share a few letters and nothing else. Tuned to be generous: a suggestion
    /// nobody wanted is one keystroke to ignore, and a missing one is a duplicate created.
    /// </summary>
    public const double MinimumSimilarity = 0.3;

    /// <summary>
    /// Above this, two names are the same thing spelled differently - see
    /// <see cref="NameSuggestion.Similarity"/>. Used by callers deciding whether to propose a merge
    /// rather than a completion.
    /// </summary>
    public const double DuplicateSimilarity = 0.6;

    /// <summary>Five: a list under a text field that has to be readable at a glance while somebody types.</summary>
    private const int Limit = 5;

    /// <summary>
    /// Shorter than this and everything looks similar to everything, so the list is noise and the query
    /// is wasted. Two characters is where a name starts being a guess about a specific thing.
    /// </summary>
    private const int ShortestUsefulQuery = 2;

    private readonly INameSuggestionRepository _nameSuggestionRepository;

    public GetNameSuggestionsQueryHandler(INameSuggestionRepository nameSuggestionRepository)
    {
        _nameSuggestionRepository = nameSuggestionRepository;
    }

    public async Task<IReadOnlyList<NameSuggestion>> HandleAsync(
        GetNameSuggestionsQuery request, CancellationToken cancellationToken)
    {
        var typed = request.Typed.Trim();
        if (typed.Length < ShortestUsefulQuery)
        {
            return [];
        }

        var found = await _nameSuggestionRepository.FindAsync(
            request.UserId, request.Kind, typed, MinimumSimilarity, Limit, cancellationToken);

        // What was already typed is not a suggestion. Offering it back is the one result guaranteed to
        // be useless, and it is the one most likely to come first.
        return [.. found.Where(suggestion => !string.Equals(suggestion.Name, typed, StringComparison.CurrentCultureIgnoreCase))];
    }
}
