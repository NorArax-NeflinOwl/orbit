using Orbit.Contracts.Notes;
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
            line.Text, line.IsChecklistItem, line.IsChecked, line.IsFailed, StyleOf(line.Style)))];

    public static IReadOnlyList<NoteContentLineDto> ToDtos(this IEnumerable<NoteContentLine> lines)
        => [.. lines.Select(line => new NoteContentLineDto(
            line.Text, line.IsChecklistItem, line.IsChecked, line.IsFailed, line.Style.ToString()))];

    /// <summary>
    /// The style of a line that has not been brought over to the surface - the pages that only read a
    /// note hold the lines as they arrived, and still have to draw a heading as a heading.
    /// </summary>
    public static NoteLineStyle StyleOf(this NoteContentLineDto line) => StyleOf(line.Style);

    /// <summary>A style read off the wire - see NoteLineStyles.Read, which is where that rule lives.</summary>
    private static NoteLineStyle StyleOf(string? style) => NoteLineStyles.Read(style);
}
