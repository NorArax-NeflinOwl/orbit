namespace Orbit.Contracts.Tags;

/// <summary>
/// The colour one of the account's tags is drawn in - see Orbit.Core.Tags.TagColour. <paramref name="Colour"/>
/// is "#rrggbb"; a tag with no row here is drawn plain.
/// </summary>
public sealed record TagColourDto(string Tag, string Colour);
