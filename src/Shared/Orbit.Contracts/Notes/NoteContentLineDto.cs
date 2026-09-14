namespace Orbit.Contracts.Notes;

/// <summary>One line of a note's content - either plain text, or a checklist item with its checked state.</summary>
/// <param name="IsFailed">
/// Crossed out rather than ticked - see Orbit.Core.Notes.NoteContentLine.IsFailed. Defaulted and last:
/// a client written before the cross existed sends nothing here, which reads as a line nobody crossed
/// out, and one reading a crossed-out line without knowing the field sees a line still to do.
/// </param>
/// <param name="Style">
/// What the line is - "Body", "Title", "Heading", "Subheading", "Monospaced", "Bulleted", "Dashed" or
/// "Numbered". See Orbit.Core.Notes.NoteLineStyle. Sent as the word rather than the number, the way every
/// other enum on this wire is, so a build that does not know a style can say which one it did not know.
///
/// Defaulted and last for the reason IsFailed is: a client written before styles existed sends nothing
/// here and its lines read as Body, which is what they were. A word this build does not know reads as
/// Body too - a line drawn plainly is a line, where a refused save would lose the writing.
/// </param>
public sealed record NoteContentLineDto(
    string Text, bool IsChecklistItem, bool IsChecked, bool IsFailed = false, string Style = "Body");
