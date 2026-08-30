namespace Orbit.Core.Suggestions;

public interface INameSuggestionRepository
{
    /// <summary>
    /// Names of this kind the reader already owns that look like <paramref name="typed"/>, closest
    /// first, at most <paramref name="limit"/> of them.
    ///
    /// Scoped to what the caller owns, which is the whole reason this is worth having: a suggestion is
    /// only useful if it is a name that exists in their data, and only safe if it is a name they were
    /// entitled to see. Nothing here reaches another account's rows.
    /// </summary>
    Task<IReadOnlyList<NameSuggestion>> FindAsync(
        Guid userId, NameSuggestionKind kind, string typed, double minimumSimilarity, int limit,
        CancellationToken cancellationToken);
}
