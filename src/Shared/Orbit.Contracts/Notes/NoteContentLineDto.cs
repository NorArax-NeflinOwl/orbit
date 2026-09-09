namespace Orbit.Contracts.Notes;

/// <summary>One line of a note's content - either plain text, or a checklist item with its checked state.</summary>
/// <param name="IsFailed">
/// Crossed out rather than ticked - see Orbit.Core.Notes.NoteContentLine.IsFailed. Defaulted and last:
/// a client written before the cross existed sends nothing here, which reads as a line nobody crossed
/// out, and one reading a crossed-out line without knowing the field sees a line still to do.
/// </param>
public sealed record NoteContentLineDto(string Text, bool IsChecklistItem, bool IsChecked, bool IsFailed = false);
