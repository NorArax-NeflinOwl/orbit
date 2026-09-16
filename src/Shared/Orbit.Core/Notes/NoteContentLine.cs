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
/// <param name="Table">
/// The table this line is, when it is one - see <see cref="NoteTable"/>, and <see cref="IsATable"/>.
/// What kind of line this is, is said by what it carries rather than by a separate word that could
/// disagree with it: a line with a table is the table, and its <paramref name="Text"/> is empty. A
/// picture will be carried the same way. Null for ordinary writing, and for every line saved before
/// tables existed.
/// </param>
/// <param name="Picture">
/// The picture this line is, when it is one - see <see cref="NotePictureLine"/>, carried the way a table
/// is. Null for everything else.
/// </param>
/// <param name="Separator">
/// The rule this line is, when it is one - see <see cref="NoteSeparatorLine"/>, carried the way the two
/// above are. A separator is not a style, because it has no words to style: it is what the line is.
/// Null for everything else, and for every line stored before separators existed.
/// </param>
public sealed record NoteContentLine(
    string Text, bool IsChecklistItem, bool IsChecked, bool IsFailed = false,
    NoteLineStyle Style = NoteLineStyle.Body,
    IReadOnlyList<NoteTextRun>? Marks = null,
    NoteTable? Table = null,
    NotePictureLine? Picture = null,
    NoteSeparatorLine? Separator = null)
{
    /// <summary>A line that is a picture and nothing else - see <see cref="OfTable"/>, which is the same rule for a table.</summary>
    public static NoteContentLine OfPicture(NotePictureLine picture)
        => new(string.Empty, IsChecklistItem: false, IsChecked: false, Picture: picture);

    /// <summary>Whether this line is a picture rather than writing.</summary>
    public bool IsAPicture => Picture is not null;

    /// <summary>
    /// Whether this line is something other than words - a table, a picture or a rule across the note.
    /// What every rule that counts characters asks: such a line has no caret offset to speak of, so an
    /// edit that split or joined it as if it had words would be the caret landing nowhere. They differ
    /// in one place only, which <see cref="IsTakenAwayByAKey"/> names.
    /// </summary>
    public bool IsAnElement => IsATable || IsAPicture || IsASeparator;

    /// <summary>
    /// Whether Backspace or Delete over this line takes it away. A picture and a rule do, as any element
    /// in a page does; a table does not, because a key that could quietly delete a grid of words is not
    /// a key, and its own menu takes it away instead.
    /// </summary>
    public bool IsTakenAwayByAKey => IsAPicture || IsASeparator;

    /// <summary>
    /// A line that is a rule across the note and nothing else - the one way a separator line is made,
    /// so nothing can make one that also carries words. <paramref name="stamp"/> is written once, here,
    /// and never worked out again - see <see cref="NoteSeparatorLine.Stamp"/>.
    /// </summary>
    public static NoteContentLine OfSeparator(string stamp)
        => new(string.Empty, IsChecklistItem: false, IsChecked: false, Separator: new NoteSeparatorLine(stamp));

    /// <summary>Whether this line is a rule across the note rather than writing.</summary>
    public bool IsASeparator => Separator is not null;

    /// <summary>
    /// A line that is a table and nothing else: no words, no box, ordinary style. The one way a table
    /// line is made, so nothing can make one that also carries words.
    /// </summary>
    public static NoteContentLine OfTable(NoteTable table)
        => new(string.Empty, IsChecklistItem: false, IsChecked: false, Table: NoteTables.Squared(table));

    /// <summary>
    /// Whether this line is a table rather than writing. Every rule on the surface that counts
    /// characters asks this first: a table has no caret offset to speak of, and an edit that split or
    /// joined it as if it had words would be the caret landing nowhere.
    /// </summary>
    public bool IsATable => Table is not null;

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
            && AllMarks.SequenceEqual(other.AllMarks)
            && Equals(Table, other.Table)
            && Picture == other.Picture
            && Separator == other.Separator;

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

        hash.Add(Table);
        hash.Add(Picture);
        hash.Add(Separator);
        return hash.ToHashCode();
    }
}
