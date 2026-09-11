using Orbit.Core.Tags;

namespace Orbit.Mobile.Screens.Tags;

/// <summary>
/// One tag on a card, with the colour the account gave it - see LocalTagColourRepository. An empty colour
/// is a tag nobody coloured, drawn plain.
/// </summary>
public sealed record TagChip(string Name, string Colour)
{
    public bool HasColour => Colour.Length > 0;
}

/// <summary>
/// A card's tags, as one object the row carries - so the control drawing them can be bound to it with a
/// type it knows (see TagChipsView) rather than to a bare list.
/// </summary>
public sealed record TagChips(IReadOnlyList<TagChip> Chips)
{
    public static readonly TagChips None = new([]);

    public bool HasAny => Chips.Count > 0;

    /// <param name="colours">The account's colours by tag key (TagNames.KeyOf), or null to draw every tag plain.</param>
    public static TagChips For(IReadOnlyList<string> tags, IReadOnlyDictionary<string, string>? colours)
        => tags.Count == 0
            ? None
            : new([
                .. tags.Select(tag => new TagChip(
                    tag,
                    colours is not null && colours.TryGetValue(TagNames.KeyOf(tag), out var colour) ? colour : string.Empty))
            ]);
}
