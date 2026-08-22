namespace Orbit.Core.Notes;

/// <summary>
/// One line of a note's content - either plain text, or a checklist item with its checked state. A
/// note's Content is an ordered list of these, persisted as JSON (see NoteEntity.ContentJson) rather
/// than free-form text, so a checklist item's checked state is a real field instead of "[ ]"/"[x]"
/// text a client has to parse back out.
/// </summary>
public sealed record NoteContentLine(string Text, bool IsChecklistItem, bool IsChecked)
{
    /// <summary>Shorthand for a plain (non-checklist) line - the shape most existing content, and most tests, actually need.</summary>
    public static NoteContentLine PlainText(string text) => new(text, IsChecklistItem: false, IsChecked: false);
}
