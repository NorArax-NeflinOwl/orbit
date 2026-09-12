using Orbit.Contracts.Tags;
using Orbit.Core.Tags;

namespace Orbit.Web.Services;

/// <summary>
/// The colours this account gives its tags, read once and shared by every card and field on the page that
/// draws a tag - see Orbit.Core.Tags.TagColour. A colour belongs to the word for the whole account, so a
/// colour set in one editor is the colour on every card: the book says when it changed, and whatever draws
/// tags draws them again.
/// </summary>
public sealed class TagColourBook(TagsApiClient tagsApiClient)
{
    private Dictionary<string, TagColourDto> _byKey = new(StringComparer.Ordinal);
    private Task? _loading;

    /// <summary>Raised whenever the colours change - after the first read, and after every colour set.</summary>
    public event Action? Changed;

    /// <summary>Every tag the account has given a colour, as it was written when it got it.</summary>
    public IReadOnlyList<string> ColouredTags => [.. _byKey.Values.Select(colour => colour.Tag)];

    /// <summary>Reads the colours the first time anything asks, and hands every later caller the same read.</summary>
    public Task LoadAsync() => _loading ??= LoadOnceAsync();

    /// <summary>
    /// The colour this tag is drawn in, or null for one drawn plain. Only ever a "#rrggbb" colour: it is
    /// written into a style attribute, so anything else the server might hand back is not trusted to be
    /// only a colour.
    /// </summary>
    public string? ColourOf(string tag)
        => _byKey.TryGetValue(TagNames.KeyOf(tag), out var colour) && TagColour.IsAColour(colour.Colour)
            ? colour.Colour
            : null;

    /// <summary>Gives the tag this colour for the whole account, or takes it away with an empty one. Throws when it did not save.</summary>
    public async Task SetAsync(string tag, string colour)
        => Replace(await tagsApiClient.SetColourAsync(tag, colour));

    private async Task LoadOnceAsync() => Replace(await tagsApiClient.GetColoursAsync());

    private void Replace(IReadOnlyList<TagColourDto> colours)
    {
        _byKey = colours
            .GroupBy(colour => TagNames.KeyOf(colour.Tag), StringComparer.Ordinal)
            .ToDictionary(sameTag => sameTag.Key, sameTag => sameTag.Last(), StringComparer.Ordinal);
        Changed?.Invoke();
    }
}
