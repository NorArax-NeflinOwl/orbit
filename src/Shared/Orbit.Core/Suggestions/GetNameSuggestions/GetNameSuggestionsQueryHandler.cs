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
        if (request.Kind == NameSuggestionKind.TaskItemDescription)
        {
            found = await WithTheirSourcesAsync(request.UserId, found, cancellationToken);
        }

        // What was already typed is not a suggestion - offering it back is the one result guaranteed to be
        // useless - unless it is the name of something the entry can be made the same thing as. Then it
        // is the most useful one: somebody typed the whole name and now says which thing they meant.
        return [.. found.Where(suggestion =>
            suggestion.Sources.Count > 0
            || !string.Equals(suggestion.Name, typed, StringComparison.CurrentCultureIgnoreCase))];
    }

    /// <summary>A few things of one name at most: past that the list stops being readable at a glance.</summary>
    private const int MostSourcesPerName = 3;

    /// <summary>
    /// What each name is the name of, for a task entry to be made the same thing as - see
    /// NameSuggestion.Sources. Only a task entry's field asks: nothing else can be a reference.
    /// </summary>
    private async Task<IReadOnlyList<NameSuggestion>> WithTheirSourcesAsync(
        Guid userId, IReadOnlyList<NameSuggestion> found, CancellationToken cancellationToken)
    {
        if (found.Count == 0)
        {
            return found;
        }

        var sources = await _nameSuggestionRepository.FindSourcesAsync(
            userId, [.. found.Select(suggestion => suggestion.Name)], cancellationToken);
        return [.. found.Select(suggestion => suggestion with
        {
            Sources = [.. sources
                .Where(source => string.Equals(source.Name, suggestion.Name, StringComparison.CurrentCultureIgnoreCase))
                .Take(MostSourcesPerName)]
        })];
    }
}
