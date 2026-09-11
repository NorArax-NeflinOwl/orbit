using Orbit.Contracts.Notes;
using Orbit.Core.Notes;

namespace Orbit.Web.Services;

/// <summary>
/// Between the lines a note travels as (NoteContentLineDto) and the lines its writing surface is worked
/// out on (Orbit.Core's NoteContentLine - see SurfaceState, which lives there so the phone decides its
/// edits with the same rules). The two have the same four fields, so this only changes the type.
/// </summary>
public static class NoteSurfaceLines
{
    public static IReadOnlyList<NoteContentLine> ToSurfaceLines(this IEnumerable<NoteContentLineDto> lines)
        => [.. lines.Select(line => new NoteContentLine(line.Text, line.IsChecklistItem, line.IsChecked, line.IsFailed))];

    public static IReadOnlyList<NoteContentLineDto> ToDtos(this IEnumerable<NoteContentLine> lines)
        => [.. lines.Select(line => new NoteContentLineDto(line.Text, line.IsChecklistItem, line.IsChecked, line.IsFailed))];
}
