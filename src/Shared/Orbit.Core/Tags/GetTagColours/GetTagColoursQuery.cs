using Orbit.Core.Abstractions;

namespace Orbit.Core.Tags.GetTagColours;

/// <summary>Every colour the caller has given one of their tags - see <see cref="TagColour"/>.</summary>
public sealed record GetTagColoursQuery(Guid UserId) : IRequest<IReadOnlyList<TagColour>>;
