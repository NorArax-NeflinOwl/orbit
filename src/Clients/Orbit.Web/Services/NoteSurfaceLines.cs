using Orbit.Contracts.Notes;
using Orbit.Contracts.Sharing;
using Orbit.Core.Notes;

namespace Orbit.Web.Services;

/// <summary>
/// Between the lines a note travels as (NoteContentLineDto) and the lines its writing surface is worked
/// out on (Orbit.Core's NoteContentLine - see SurfaceState, which lives there so the phone decides its
/// edits with the same rules). The same fields on both sides; only the style changes shape, because it
/// travels as a word and is an enum once it is here.
/// </summary>
public static class NoteSurfaceLines
{
    public static IReadOnlyList<NoteContentLine> ToSurfaceLines(this IEnumerable<NoteContentLineDto> lines)
        => [.. lines.Select(line => new NoteContentLine(
            line.Text, line.IsChecklistItem, line.IsChecked, line.IsFailed, StyleOf(line.Style),
            MarksOf(line)))];

    public static IReadOnlyList<NoteContentLineDto> ToDtos(this IEnumerable<NoteContentLine> lines)
        => [.. lines.Select(line => new NoteContentLineDto(
            line.Text, line.IsChecklistItem, line.IsChecked, line.IsFailed, line.Style.ToString(),
            MarksSent(line)))];

    /// <summary>
    /// A line's marks as they go out - null rather than an empty list for a line with none, which is
    /// nearly every line of nearly every note: the field is then simply absent from the JSON.
    /// </summary>
    private static IReadOnlyList<NoteTextRunDto>? MarksSent(NoteContentLine line)
        => line.AllMarks.Count == 0
            ? null
            : line.AllMarks.Select(run => new NoteTextRunDto(run.Start, run.Length, run.Mark.ToString())).ToList();

    /// <summary>
    /// A line's marks, read off the wire and put in the one shape the rules work in - see
    /// NoteTextMarks.Normalized, which also drops a mark this build does not know. Public because the
    /// pages that only <em>read</em> a note need them too, to draw a line's words as they were written.
    /// </summary>
    public static IReadOnlyList<NoteTextRun> MarksOf(this NoteContentLineDto line)
        => MarksOf(line.AllMarks, line.Text);

    /// <inheritdoc cref="MarksOf(NoteContentLineDto)"/>
    public static IReadOnlyList<NoteTextRun> MarksOf(this PublicSharedItemLineDto line)
        => MarksOf(line.AllMarks, line.Text);

    private static IReadOnlyList<NoteTextRun> MarksOf(IReadOnlyList<NoteTextRunDto> marks, string text)
        => NoteTextMarks.Normalized(
            marks.Select(run => new NoteTextRun(run.Start, run.Length, NoteTextMarks.Read(run.Mark))),
            text.Length);

    /// <summary>
    /// The style of a line that has not been brought over to the surface - the pages that only read a
    /// note hold the lines as they arrived, and still have to draw a heading as a heading.
    /// </summary>
    public static NoteLineStyle StyleOf(this NoteContentLineDto line) => StyleOf(line.Style);

    /// <summary>A style read off the wire - see NoteLineStyles.Read, which is where that rule lives.</summary>
    private static NoteLineStyle StyleOf(string? style) => NoteLineStyles.Read(style);
}
