using Orbit.Core.Abstractions;

namespace Orbit.Core.Notes;

/// <summary>
/// A note as plain words - what "copy the text" puts on the clipboard. Shared rather than written once
/// per client, because the two would drift and a note copied on a phone would paste differently from the
/// same note copied in a browser.
///
/// The name is the first line, since that is what a note's name is on both clients: the first line of
/// the writing. A ticked box is written "[x] " and an unticked one "- ", and both are read back as boxes
/// by a paste (NoteSurfaceEdits.ReadPastedLine - "[x]" ticked, "- " not), so a note copied here and
/// pasted into another arrives as the same note rather than as a page of brackets.
///
/// A line that is not words at all - a table, a picture, a rule across the note - is left out rather than
/// written as a blank: a table's words are in its cells, and a picture has none. A rule is left out even
/// where it carries a stamp, because what is pasted is read back as lines (NoteSurfaceEdits.ReadPastedLine)
/// and nothing there makes a rule: written out, it would come back as a line of words pretending to be one.
/// </summary>
public static class NoteWords
{
    /// <summary>
    /// The note as it reads, name first - see the type, which says what each line becomes.
    /// <paramref name="what"/> narrows it to the boxes in one state, for the reader who wants the
    /// shopping still to do rather than the whole page (see <see cref="WhatToCopy"/>). The name stays
    /// whichever is asked for: a list of errands with nothing saying which list is one nobody can place.
    /// </summary>
    public static string Of(
        string title, IEnumerable<NoteContentLine> lines, WhatToCopy what = WhatToCopy.Everything)
        => string.Join(
            "\n",
            new[] { title }.Concat(lines.Where(line => !line.IsAnElement).Where(line => what.Keeps(line)).Select(AsALine)));

    /// <summary>
    /// Whether this line belongs in a copy of <paramref name="what"/>. A line with no box is not in any
    /// of the three states the narrow choices ask about, so it travels only in the whole thing.
    /// </summary>
    private static bool Keeps(this WhatToCopy what, NoteContentLine line)
        => line.IsChecklistItem
            ? what.Keeps(Ticks.Read(line.IsChecked, line.IsFailed))
            : what.KeepsWhatHasNoBox();

    /// <summary>
    /// One line as the clipboard carries it. A crossed-out box goes out as an unticked one and not as a
    /// mark of its own: the format is only worth having because a paste reads it back
    /// (NoteSurfaceEdits.ReadPastedLine), and that knows a box and a ticked box and nothing else. A
    /// third marker would paste in as words pretending to be a line.
    /// </summary>
    private static string AsALine(NoteContentLine line)
        => line switch
        {
            { IsChecklistItem: false } => line.Text,
            { IsChecked: true } => $"[x] {line.Text}",
            _ => $"- {line.Text}"
        };
}
