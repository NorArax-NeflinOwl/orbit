namespace Orbit.Contracts.Notes;

/// <summary>One line of a note's content - either plain text, or a checklist item with its checked state.</summary>
public sealed record NoteContentLineDto(string Text, bool IsChecklistItem, bool IsChecked);
