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
/// A line that is not words at all - a table, a picture - is left out rather than written as a blank:
/// a table's words are in its cells, and a picture has none.
/// </summary>
public static class NoteWords
{
    public static string Of(string title, IEnumerable<NoteContentLine> lines)
        => string.Join(
            "\n",
            new[] { title }.Concat(lines.Where(line => !line.IsAnElement).Select(AsALine)));

    private static string AsALine(NoteContentLine line)
        => line switch
        {
            { IsChecklistItem: false } => line.Text,
            { IsChecked: true } => $"[x] {line.Text}",
            _ => $"- {line.Text}"
        };
}
