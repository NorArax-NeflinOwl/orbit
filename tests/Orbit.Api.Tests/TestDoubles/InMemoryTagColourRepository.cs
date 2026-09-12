using Orbit.Core.Tags;

namespace Orbit.Api.Tests.TestDoubles;

/// <summary>
/// Keyed the way the real table is (account, TagNames.KeyOf), so "Work" and "work" are one row here too -
/// a double that kept both would let a client that colours one spelling look right against it.
/// </summary>
public sealed class InMemoryTagColourRepository : ITagColourRepository
{
    private readonly Dictionary<(Guid UserId, string Key), TagColour> _colours = [];

    public Task<IReadOnlyList<TagColour>> GetAllAsync(Guid userId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<TagColour>>(
        [
            .. _colours
                .Where(entry => entry.Key.UserId == userId)
                .OrderBy(entry => entry.Key.Key, StringComparer.Ordinal)
                .Select(entry => entry.Value)
        ]);

    public Task SetAsync(Guid userId, string tag, string colour, CancellationToken cancellationToken)
    {
        _colours[(userId, TagNames.KeyOf(tag))] = new TagColour(tag.Trim(), colour);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(Guid userId, string tag, CancellationToken cancellationToken)
    {
        _colours.Remove((userId, TagNames.KeyOf(tag)));
        return Task.CompletedTask;
    }
}
