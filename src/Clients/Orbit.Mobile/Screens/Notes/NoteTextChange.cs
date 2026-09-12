namespace Orbit.Mobile.Screens.Notes;

/// <summary>
/// What one change to a line's text did: where it happened, how many characters of the old text went,
/// and what came in their place. Worked out from the text before and after it, because a field on the
/// phone reports only what it says now - it has no event for a key, a paste or a word taken from the
/// keyboard's suggestions, and all three arrive as the same "the text changed".
///
/// The common start and the common end of the two are kept and the middle is the change, which is exact
/// for everything but a character typed into a run of the same character ("aa" to "aaa"): that reads as
/// typed at the end of the run. Only the caret an undo puts back depends on it, and it lands a few
/// characters along in the same line.
/// </summary>
public readonly record struct NoteTextChange(int Start, int Removed, string Inserted)
{
    public static NoteTextChange Between(string before, string after)
    {
        var shortest = Math.Min(before.Length, after.Length);

        var start = 0;
        while (start < shortest && before[start] == after[start])
        {
            start++;
        }

        var end = 0;
        while (end < shortest - start && before[before.Length - 1 - end] == after[after.Length - 1 - end])
        {
            end++;
        }

        return new NoteTextChange(start, before.Length - start - end, after[start..(after.Length - end)]);
    }
}
