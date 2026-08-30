using Orbit.Core.Suggestions;

namespace Orbit.Api.Tests.TestDoubles;

/// <summary>
/// Stands in for the trigram search, which is PostgreSQL's to do (see NameSuggestionRepository). Scores
/// by shared characters rather than by real trigrams - close enough to order a handful of names, and
/// the handler's own rules are what these tests are about.
/// </summary>
internal sealed class InMemoryNameSuggestionRepository : INameSuggestionRepository
{
    private readonly Dictionary<NameSuggestionKind, List<string>> _namesByKind = [];

    public void Add(NameSuggestionKind kind, params string[] names)
    {
        if (!_namesByKind.TryGetValue(kind, out var existing))
        {
            existing = [];
            _namesByKind[kind] = existing;
        }

        existing.AddRange(names);
    }

    /// <summary>What the last call was asked for, so a test can prove a query never reached the database.</summary>
    public List<string> Queries { get; } = [];

    public Task<IReadOnlyList<NameSuggestion>> FindAsync(
        Guid userId, NameSuggestionKind kind, string typed, double minimumSimilarity, int limit,
        CancellationToken cancellationToken)
    {
        Queries.Add(typed);

        IReadOnlyList<NameSuggestion> found =
        [
            .. _namesByKind.GetValueOrDefault(kind, [])
                .Select(name => new NameSuggestion(name, Similarity(name, typed)))
                .Where(suggestion => suggestion.Similarity >= minimumSimilarity)
                .OrderByDescending(suggestion => suggestion.Similarity)
                .ThenBy(suggestion => suggestion.Name)
                .Take(limit)
        ];

        return Task.FromResult(found);
    }

    private static double Similarity(string name, string typed)
    {
        var left = name.Trim().ToLowerInvariant();
        var right = typed.Trim().ToLowerInvariant();
        if (left.Length == 0 || right.Length == 0)
        {
            return 0;
        }

        var shared = right.Distinct().Count(character => left.Contains(character));
        return (double)shared / Math.Max(left.Distinct().Count(), right.Distinct().Count());
    }
}
