namespace Orbit.Core.Tags;

/// <summary>An account's tag colours - see <see cref="TagColour"/>. Tags are matched by <see cref="TagNames.KeyOf"/>.</summary>
public interface ITagColourRepository
{
    /// <summary>Every colour this account has chosen, in no order a reader relies on.</summary>
    Task<IReadOnlyList<TagColour>> GetAllAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Gives this tag this colour, replacing any it had - and writes the tag as given this time.</summary>
    Task SetAsync(Guid userId, string tag, string colour, CancellationToken cancellationToken);

    /// <summary>Takes this tag's colour away, so it is drawn plain again. Nothing to do for one that has none.</summary>
    Task RemoveAsync(Guid userId, string tag, CancellationToken cancellationToken);
}
