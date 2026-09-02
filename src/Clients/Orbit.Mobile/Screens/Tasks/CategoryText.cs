namespace Orbit.Mobile.Screens.Tasks;

/// <summary>
/// Categories as one line of text - "shopping, car" - which is how they are typed and read on a form.
/// A shelf item's category is a single box, and a task entry's is the same box holding as many as
/// apply, rather than a control of its own to learn. The same rules Orbit.Web reads them by - see its
/// CategoryText - so a line typed on either client comes back the same on the other.
///
/// The tidying the domain applies on the way in is applied here too, so what the reader sees after
/// typing is what will actually be stored.
/// </summary>
public static class CategoryText
{
    public static string Join(IEnumerable<string> categories) => string.Join(", ", categories);

    public static IReadOnlyList<string> Split(string? text)
        => text is null
            ? []
            : [.. text
                .Split(',')
                .Select(category => category.Trim())
                .Where(category => category.Length > 0)
                .Distinct(StringComparer.CurrentCultureIgnoreCase)];
}
