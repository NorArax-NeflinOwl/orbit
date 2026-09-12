using Orbit.Core.Abstractions;

namespace Orbit.Core.Tags.SetTagColour;

/// <summary>
/// Gives one of the caller's tags a colour, or takes it away with an empty <paramref name="Colour"/> -
/// see <see cref="TagColour"/>. Answers every colour the account has afterwards, so a client can draw
/// from what the server now holds rather than from what it asked for.
/// </summary>
[ClientAction(ClientActionCategory.Edit)]
public sealed record SetTagColourCommand(Guid UserId, string Tag, string Colour) : IRequest<IReadOnlyList<TagColour>>;
