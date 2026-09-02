namespace Orbit.Core.Tasks;

/// <summary>
/// Categories as one line of text - "shopping, car" - which is how they are typed and read on a form.
/// A shelf item's category is a single box, and a task entry's is the same box holding as many as
/// apply, rather than a control of its own to learn.
///
/// The same tidying <see cref="TaskItem.Categories"/> applies on the way in, done here as well so what
/// the reader sees after typing is what will actually be stored. Here rather than in a client, because
/// both of them have this box and one rule is what stops the browser and the phone disagreeing about
/// what "shopping, Shopping" means.
/// </summary>
public static class CategoryText
{
    public static string Join(IEnumerable<string> categories) => string.Join(", ", categories);

    public static List<string> Split(string? text)
        => text is null
            ? []
            : [.. text
                .Split(',')
                .Select(category => category.Trim())
                .Where(category => category.Length > 0)
                .Distinct(StringComparer.CurrentCultureIgnoreCase)];
}
