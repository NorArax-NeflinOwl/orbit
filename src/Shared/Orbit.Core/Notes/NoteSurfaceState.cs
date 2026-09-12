using System.Text.Json.Serialization;

namespace Orbit.Core.Notes;

/// <summary>
/// A place in the writing: which line, and how many characters into its text. Counted in UTF-16 units,
/// which is what both a JavaScript string and a .NET one count in, so the browser and this side agree on
/// where a caret is without translating.
/// </summary>
public sealed record SurfacePoint(int Line, int Offset) : IComparable<SurfacePoint>
{
    public int CompareTo(SurfacePoint? other)
        => other is null ? 1 : Line != other.Line ? Line.CompareTo(other.Line) : Offset.CompareTo(other.Offset);
}

/// <summary>
/// Everything the note's writing surface holds at one moment: its lines, and the selection in them.
/// Anchor is where a selection was started and Focus where it ends up - the browser's own two words - and
/// the two are the same point when there is only a caret.
///
/// This is what every edit that changes the shape of the writing is worked out on (see NoteSurfaceEdits),
/// and what the undo history keeps a copy of (see NoteSurfaceHistory). The browser only reports it and
/// draws what comes back - see checklistTextEditor.js.
///
/// Here in Orbit.Core rather than in either client because both write notes with it: Orbit.Web's
/// ChecklistTextEditor and the phone's NoteDetailViewModel, which reads pastes, answers a press on several
/// boxes and keeps its undo history with the same rules. The lines are the domain's NoteContentLine, the
/// same shape as the wire's NoteContentLineDto, so each client converts at its own edge.
/// </summary>
/// <remarks>
/// Only the three positional members travel to the browser; the rest are worked out from them and are
/// kept out of the JSON.
/// </remarks>
public sealed record SurfaceState(IReadOnlyList<NoteContentLine> Lines, SurfacePoint Anchor, SurfacePoint Focus)
{
    [JsonIgnore]
    public SurfacePoint Start => Anchor.CompareTo(Focus) <= 0 ? Anchor : Focus;

    [JsonIgnore]
    public SurfacePoint End => Anchor.CompareTo(Focus) <= 0 ? Focus : Anchor;

    [JsonIgnore]
    public bool IsCollapsed => Anchor == Focus;

    /// <summary>The caret - where a collapsed selection is, or where the reader last moved to in one that is not.</summary>
    [JsonIgnore]
    public SurfacePoint Caret => Focus;

    public static SurfaceState CaretAt(IReadOnlyList<NoteContentLine> lines, SurfacePoint caret)
        => new(lines, caret, caret);

    /// <summary>
    /// The lines a selection covers. A selection that ends at the very start of a line does not cover
    /// that line - dragging down to the head of the next line, or pressing Shift+Down, marks the lines
    /// above it and nothing of the one it stops on.
    /// </summary>
    [JsonIgnore]
    public (int First, int Last) SelectedLines
        => (Start.Line, End.Offset == 0 && End.Line > Start.Line ? End.Line - 1 : End.Line);

    /// <summary>
    /// The same state made safe to work on: at least one line, and both points inside the text they
    /// name. What the browser reports is read off a document that can be half way through changing, so
    /// nothing downstream has to wonder about an index past the end.
    /// </summary>
    public SurfaceState Normalized()
    {
        IReadOnlyList<NoteContentLine> lines = Lines.Count > 0 ? Lines : [EmptyLine];
        return new SurfaceState(lines, Clamp(Anchor, lines), Clamp(Focus, lines));
    }

    public static readonly NoteContentLine EmptyLine = new(string.Empty, IsChecklistItem: false, IsChecked: false);

    private static SurfacePoint Clamp(SurfacePoint point, IReadOnlyList<NoteContentLine> lines)
    {
        var line = Math.Clamp(point.Line, 0, lines.Count - 1);
        return new SurfacePoint(line, Math.Clamp(point.Offset, 0, lines[line].Text.Length));
    }
}
