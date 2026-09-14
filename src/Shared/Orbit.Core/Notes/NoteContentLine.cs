namespace Orbit.Core.Notes;

/// <summary>
/// One line of a note's content - either plain text, or a checklist item with its checked state. A
/// note's Content is an ordered list of these, persisted as JSON (see NoteEntity.ContentJson) rather
/// than free-form text, so a checklist item's checked state is a real field instead of "[ ]"/"[x]"
/// text a client has to parse back out.
/// </summary>
/// <param name="IsFailed">
/// Crossed out rather than ticked: the line was finished with and not done - the same three states a
/// task entry has, see Orbit.Core.Tasks.TaskItem.IsFailed. Defaulted and last, so content stored before
/// the cross existed reads as a line nobody crossed out; never true together with
/// <paramref name="IsChecked"/>, which is settled by whoever writes the line.
/// </param>
/// <param name="Style">
/// What the line is - a heading, a line of a list, ordinary writing. See <see cref="NoteLineStyle"/>.
/// Defaulted and last for the reason <paramref name="IsFailed"/> is: content stored before styles
/// existed reads back as Body, which is what every line of it was. A note's content is JSON on both
/// clients (NoteEntity.ContentJson, and the phone's own store), so that costs no migration - the field
/// simply is not there in what was written before, and the record's default answers for it.
/// </param>
/// <param name="Marks">
/// The marks on stretches of words <em>inside</em> this line - bold, italic, underlined, struck through.
/// See <see cref="NoteTextRun"/>, and <see cref="NoteTextMarks"/> for what happens to them when the words
/// move. Null rather than an empty list, because a default has to be a constant: read it through
/// <see cref="AllMarks"/>, which answers the same for a line that has none and a line saved before marks
/// existed. Kept beside the text rather than folded into it, so everything that reads a line's words -
/// every search, every preview, every copy - goes on reading a plain string.
/// </param>
public sealed record NoteContentLine(
    string Text, bool IsChecklistItem, bool IsChecked, bool IsFailed = false,
    NoteLineStyle Style = NoteLineStyle.Body,
    IReadOnlyList<NoteTextRun>? Marks = null)
{
    /// <summary>Shorthand for a plain (non-checklist) line - the shape most existing content, and most tests, actually need.</summary>
    public static NoteContentLine PlainText(string text) => new(text, IsChecklistItem: false, IsChecked: false);

    /// <summary>The marks as something to read without a null check - see <see cref="Marks"/>.</summary>
    public IReadOnlyList<NoteTextRun> AllMarks => Marks ?? NoteTextMarks.None;

    /// <summary>
    /// Two lines are the same line when they say the same thing, marks included - written out because a
    /// record compares a list by <em>which list it is</em>, and two lines built separately from the same
    /// note would then never be equal. That comparison is not a nicety here: the phone tells which lines
    /// changed by it (NoteDetailViewModel.Show), and the browser's history drops an edit that changed
    /// nothing by it.
    /// </summary>
    public bool Equals(NoteContentLine? other)
        => other is not null
            && Text == other.Text
            && IsChecklistItem == other.IsChecklistItem
            && IsChecked == other.IsChecked
            && IsFailed == other.IsFailed
            && Style == other.Style
            && AllMarks.SequenceEqual(other.AllMarks);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Text);
        hash.Add(IsChecklistItem);
        hash.Add(IsChecked);
        hash.Add(IsFailed);
        hash.Add(Style);
        foreach (var run in AllMarks)
        {
            hash.Add(run);
        }

        return hash.ToHashCode();
    }
}
