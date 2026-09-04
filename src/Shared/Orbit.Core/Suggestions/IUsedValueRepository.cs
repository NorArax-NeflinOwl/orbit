namespace Orbit.Core.Suggestions;

public interface IUsedValueRepository
{
    /// <summary>
    /// Everything this reader has already filed something under for that field, in alphabetical order
    /// and without repeats.
    ///
    /// Scoped to what the caller owns, for the same reason name suggestions are: a word drawn from
    /// somebody else's rows would be telling them what that person keeps. Private items are left out
    /// throughout - what they are filed under is sealed in the owner's browser, and the column here
    /// holds nothing readable.
    /// </summary>
    Task<IReadOnlyList<string>> FindAllAsync(Guid userId, UsedValueKind kind, CancellationToken cancellationToken);
}
