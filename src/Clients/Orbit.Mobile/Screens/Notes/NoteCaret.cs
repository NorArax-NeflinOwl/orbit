namespace Orbit.Mobile.Screens.Notes;

/// <summary>
/// Where the note screen wants the caret after an edit it made itself - an undo, a paste split into
/// lines, a typed "[]" taken back out of the words. The view model has no fields to put it in, so it says
/// where and the page does it: see NoteDetailViewModel.CaretPlaced.
/// </summary>
/// <param name="Line">The line, or null for the note's name - the first line of the writing.</param>
/// <param name="Offset">How many characters into that line's text.</param>
public sealed record NoteCaret(NoteLineRow? Line, int Offset);
