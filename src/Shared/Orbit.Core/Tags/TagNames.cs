namespace Orbit.Core.Tags;

/// <summary>
/// The words a note or a task list is tagged with - the rule both kinds, both clients and the colour store
/// share. The same shape a task entry's categories take (see Orbit.Core.Tasks.TaskItem.Categories), and
/// the same tidying: blanks dropped, edges trimmed, and a word said twice kept once, whatever its case -
/// "Work" and "work" are one tag, and a filter or a colour that told them apart would split one in two.
/// </summary>
public static class TagNames
{
    /// <summary>What is worth storing of what was typed, in the order it was given.</summary>
    public static IReadOnlyList<string> Tidy(IReadOnlyList<string>? tags)
        => tags is null
            ? []
            : [.. tags
                .Select(tag => tag.Trim())
                .Where(tag => tag.Length > 0)
                .Distinct(StringComparer.CurrentCultureIgnoreCase)];

    /// <summary>
    /// What a tag is known by, whatever case it was written in - the colour store's key, so a colour set
    /// on "Work" is the colour of "work" as well.
    /// </summary>
    public static string KeyOf(string tag) => tag.Trim().ToLowerInvariant();

    /// <summary>Refuses a tag too long to store - the room a category has, since a tag is the same kind of word.</summary>
    public static void OrRefuse(IReadOnlyList<string> tags, string whose)
    {
        foreach (var tag in tags)
        {
            StoredTextLimits.OrRefuse(tag, StoredTextLimits.Category, whose);
        }
    }
}
