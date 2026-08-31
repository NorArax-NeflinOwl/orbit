using Orbit.Contracts.Notes;

namespace Orbit.Mobile.Screens.Notes;

/// <summary>What became of one line between two versions of a note.</summary>
public enum LineChange
{
    Unchanged,

    Added,

    Removed
}

/// <summary>One line as the review shows it: its words, and what happened to it.</summary>
public sealed record DiffLine(string Text, LineChange Change)
{
    public bool IsAdded => Change is LineChange.Added;

    public bool IsRemoved => Change is LineChange.Removed;

    /// <summary>
    /// The line as a diff has always been read: a sign in the margin, then the words. Colour carries the
    /// same thing, and would carry it alone to anybody who cannot tell the two colours apart.
    /// </summary>
    public string Display => Change switch
    {
        LineChange.Added => $"+ {Text}",
        LineChange.Removed => $"- {Text}",
        _ => $"  {Text}"
    };
}

/// <summary>
/// The difference between two versions of a note's lines, for the window that asks which one to keep.
///
/// Line-by-line rather than word-by-word: a note here is a list of lines, added and ticked and removed
/// one at a time, so that is the unit a reader recognises. The longest common subsequence is what keeps
/// an inserted line from re-reading every line after it as changed - the naive index-by-index compare
/// did exactly that, and turned "I added milk at the top" into "everything changed".
/// </summary>
public static class NoteVersionDiff
{
    public static IReadOnlyList<DiffLine> Between(
        IReadOnlyList<NoteContentLineDto> before, IReadOnlyList<NoteContentLineDto> after)
        => Between([.. before.Select(Describe)], [.. after.Select(Describe)]);

    /// <summary>
    /// Whether the two versions say anything different at all - what tells a review that both sides
    /// moved, which is the only case where the reader is choosing rather than just catching up.
    /// </summary>
    public static bool Differ(IReadOnlyList<NoteContentLineDto> before, IReadOnlyList<NoteContentLineDto> after)
        => Between(before, after).Any(line => line.Change is not LineChange.Unchanged);

    /// <summary>
    /// A tick is part of what a line says. Two lines reading "milk", one ticked, are not the same line,
    /// and a diff that called them equal would hide the only change somebody made all day.
    /// </summary>
    private static string Describe(NoteContentLineDto line)
        => line switch
        {
            { IsChecklistItem: true, IsChecked: true } => $"[x] {line.Text}",
            { IsChecklistItem: true } => $"[ ] {line.Text}",
            _ => line.Text
        };

    private static IReadOnlyList<DiffLine> Between(IReadOnlyList<string> before, IReadOnlyList<string> after)
    {
        var common = LongestCommonSubsequence(before, after);
        var lines = new List<DiffLine>();
        var beforeIndex = 0;
        var afterIndex = 0;

        foreach (var shared in common)
        {
            while (beforeIndex < before.Count && before[beforeIndex] != shared)
            {
                lines.Add(new DiffLine(before[beforeIndex++], LineChange.Removed));
            }

            while (afterIndex < after.Count && after[afterIndex] != shared)
            {
                lines.Add(new DiffLine(after[afterIndex++], LineChange.Added));
            }

            lines.Add(new DiffLine(shared, LineChange.Unchanged));
            beforeIndex++;
            afterIndex++;
        }

        lines.AddRange(before.Skip(beforeIndex).Select(text => new DiffLine(text, LineChange.Removed)));
        lines.AddRange(after.Skip(afterIndex).Select(text => new DiffLine(text, LineChange.Added)));
        return lines;
    }

    private static IReadOnlyList<string> LongestCommonSubsequence(
        IReadOnlyList<string> before, IReadOnlyList<string> after)
    {
        // The ordinary table: lengths[i, j] is how many lines the two tails share. Notes are short
        // enough that the whole table costs nothing, and it is the version anybody can check by eye.
        var lengths = new int[before.Count + 1, after.Count + 1];
        for (var i = before.Count - 1; i >= 0; i--)
        {
            for (var j = after.Count - 1; j >= 0; j--)
            {
                lengths[i, j] = before[i] == after[j]
                    ? lengths[i + 1, j + 1] + 1
                    : Math.Max(lengths[i + 1, j], lengths[i, j + 1]);
            }
        }

        var shared = new List<string>();
        for (int i = 0, j = 0; i < before.Count && j < after.Count;)
        {
            if (before[i] == after[j])
            {
                shared.Add(before[i]);
                i++;
                j++;
            }
            else if (lengths[i + 1, j] >= lengths[i, j + 1])
            {
                i++;
            }
            else
            {
                j++;
            }
        }

        return shared;
    }
}
