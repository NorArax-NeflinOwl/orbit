namespace Orbit.Core.Notes;

/// <summary>
/// A line drawn across the note, carried the way a table and a picture are - see
/// <see cref="NoteContentLine.Separator"/>. It has no words that can be written in, which is why it is
/// something the line carries rather than a style the line could be given.
/// </summary>
/// <param name="Stamp">
/// What is written on it - a date and time for the separator made with one, empty for a plain rule.
///
/// <b>Written once, when the separator is made</b>, and stored as the words it was made with rather
/// than as an instant. That is what somebody means by putting a date in a note - "this is where I got
/// to on Tuesday" - and it is the only reading that survives being read again: a stamp worked out at
/// draw time would make yesterday's separator say today, and nothing in the note would say when
/// anything was actually written. Being part of the line, it travels into the export, onto the phone
/// and into a shared link, and says the same thing in a year; re-formatting it in another reader's
/// locale would be re-reading it in a different way, which is the thing that decision rules out.
/// </param>
public sealed record NoteSeparatorLine(string Stamp)
{
    /// <summary>Whether anything is written on it, which is what decides how it is drawn.</summary>
    public bool HasAStamp => Stamp.Length > 0;
}
