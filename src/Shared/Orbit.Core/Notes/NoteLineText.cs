namespace Orbit.Core.Notes;

/// <summary>
/// Changing a line's words and taking its marks with them. Four shapes cover every edit this surface
/// makes - a line cut in half, a line joined to the next, a stretch replaced, a stretch taken away - and
/// each is here rather than at the call sites so the arithmetic that keeps a mark over the words it was
/// put on is written once. See <see cref="NoteTextMarks"/> for the rules themselves.
///
/// Everything here answers a whole line, because a line is what the surface works in: the text and the
/// marks over it only ever change together, and a pair that got out of step would be a bold that had
/// slid two characters left.
/// </summary>
public static class NoteLineText
{
    /// <summary>The first <paramref name="at"/> characters of the line, marks and all - the half a split leaves behind.</summary>
    public static NoteContentLine Head(this NoteContentLine line, int at)
        => line with { Text = line.Text[..at], Marks = NoteTextMarks.Before(line.AllMarks, at) };

    /// <summary>
    /// Everything from <paramref name="at"/> on, with the marks moved back to the start of it - the half
    /// a split carries down to the new line.
    /// </summary>
    public static NoteContentLine Tail(this NoteContentLine line, int at)
        => line with
        {
            Text = line.Text[at..],
            Marks = NoteTextMarks.After(line.AllMarks, at, line.Text.Length)
        };

    /// <summary>
    /// The line with <paramref name="removed"/> characters at <paramref name="at"/> replaced by
    /// <paramref name="inserted"/>. What arrives takes the mark of the character before it - see
    /// <see cref="NoteTextMarks"/>, which says why that is the rule rather than a decision.
    /// </summary>
    public static NoteContentLine Changed(this NoteContentLine line, int at, int removed, string inserted)
    {
        var text = string.Concat(line.Text.AsSpan(0, at), inserted, line.Text.AsSpan(at + removed));
        return line with
        {
            Text = text,
            Marks = NoteTextMarks.Kept(line.AllMarks, at, removed, inserted.Length, text.Length)
        };
    }

    /// <summary>
    /// This line with <paramref name="next"/>'s words on the end of it - what Backspace at the head of a
    /// line does. The line keeps everything else it is: a join takes the words, not the box or the style.
    /// </summary>
    public static NoteContentLine FollowedBy(this NoteContentLine line, NoteContentLine next)
        => line with
        {
            Text = line.Text + next.Text,
            Marks = NoteTextMarks.Joined(line.AllMarks, line.Text.Length, next.AllMarks, next.Text.Length)
        };

    /// <summary>
    /// The line with only the words from <paramref name="start"/> for <paramref name="length"/>
    /// characters - what a copy, a cut or a drag carries away.
    /// </summary>
    public static NoteContentLine Only(this NoteContentLine line, int start, int length)
        => line with
        {
            Text = line.Text.Substring(start, length),
            Marks = NoteTextMarks.Taken(line.AllMarks, start, length)
        };
}
