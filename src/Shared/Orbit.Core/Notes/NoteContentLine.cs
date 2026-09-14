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
public sealed record NoteContentLine(
    string Text, bool IsChecklistItem, bool IsChecked, bool IsFailed = false,
    NoteLineStyle Style = NoteLineStyle.Body)
{
    /// <summary>Shorthand for a plain (non-checklist) line - the shape most existing content, and most tests, actually need.</summary>
    public static NoteContentLine PlainText(string text) => new(text, IsChecklistItem: false, IsChecked: false);
}
