using System.Text.RegularExpressions;
using Orbit.Contracts.Notes;
using Orbit.Core.Abstractions;

namespace Orbit.Web.Services;

/// <summary>
/// What each edit that changes the shape of a note's writing does to it - a new line, a merged one, a
/// line taken away, a tick. Worked out here, on a <see cref="SurfaceState"/>, rather than in the browser
/// on the document itself, which is where it used to be done and where the caret bugs came from: a line
/// rebuilt in place and the caret handed to the element it replaced, a new line the caret was put in
/// before it had anywhere to stand. The browser now reports what is on the surface and draws what comes
/// back - see checklistTextEditor.js - and ordinary typing inside one line is still left to it.
///
/// A method answering null means "not this edit's business": the browser's own handling of the key is
/// right, and it is let through.
/// </summary>
public static partial class NoteSurfaceEdits
{
    /// <summary>
    /// Enter: the line is split at the caret and the caret goes to the start of the new line. A
    /// checklist line carries on as a checklist - unticked - and an empty one leaves the list instead,
    /// so pressing Enter twice ends a list rather than piling up empty boxes.
    /// </summary>
    public static SurfaceState Enter(SurfaceState state)
    {
        var cleared = DeleteSelection(state.Normalized(), forReplacement: true);
        var caret = cleared.Caret;
        var lines = cleared.Lines.ToList();
        var line = lines[caret.Line];

        if (line.IsChecklistItem && line.Text.Length == 0)
        {
            lines[caret.Line] = SurfaceState.EmptyLine;
            return SurfaceState.CaretAt(lines, new SurfacePoint(caret.Line, 0));
        }

        // At the head of a line with something on it, the new line opens above and the words stay
        // where they are - with their tick. Splitting there instead would hand the words to a fresh,
        // unticked line and leave the tick behind on an empty one.
        if (caret.Offset == 0 && line.Text.Length > 0)
        {
            lines.Insert(caret.Line, Unticked(line with { Text = string.Empty }));
            return SurfaceState.CaretAt(lines, new SurfacePoint(caret.Line + 1, 0));
        }

        lines[caret.Line] = line with { Text = line.Text[..caret.Offset] };
        lines.Insert(caret.Line + 1, Unticked(line with { Text = line.Text[caret.Offset..] }));
        return SurfaceState.CaretAt(lines, new SurfacePoint(caret.Line + 1, 0));
    }

    /// <summary>
    /// Backspace. Only at the head of a line, or over a selection that spans lines - inside a line the
    /// browser's own delete is right.
    ///
    /// At the head of a checklist line with words on it the box goes first and the words stay, the
    /// familiar "outdent before delete"; an empty checklist line has nothing to keep and goes whole, the
    /// caret to the end of the line above it. A plain line joins the one above.
    /// </summary>
    public static SurfaceState? Backspace(SurfaceState state)
    {
        state = state.Normalized();
        if (!state.IsCollapsed)
        {
            return SpansLines(state) ? DeleteSelection(state, forReplacement: false) : null;
        }

        var caret = state.Caret;
        if (caret.Offset > 0)
        {
            return null;
        }

        var lines = state.Lines.ToList();
        var line = lines[caret.Line];
        if (line.IsChecklistItem)
        {
            if (line.Text.Length == 0)
            {
                return RemoveLine(lines, caret.Line);
            }

            lines[caret.Line] = Plain(line.Text);
            return SurfaceState.CaretAt(lines, caret);
        }

        if (caret.Line == 0)
        {
            // Nothing above the first line to join. Answered rather than let through, so the browser
            // does not go looking for something outside the lines to delete.
            return state;
        }

        var previous = lines[caret.Line - 1];
        lines[caret.Line - 1] = previous with { Text = previous.Text + line.Text };
        lines.RemoveAt(caret.Line);
        return SurfaceState.CaretAt(lines, new SurfacePoint(caret.Line - 1, previous.Text.Length));
    }

    /// <summary>
    /// Delete, the mirror of <see cref="Backspace"/>: only at the end of a line, where the line below
    /// joins this one. An empty line is taken away instead, so the line below keeps its own box.
    /// </summary>
    public static SurfaceState? Delete(SurfaceState state)
    {
        state = state.Normalized();
        if (!state.IsCollapsed)
        {
            return SpansLines(state) ? DeleteSelection(state, forReplacement: false) : null;
        }

        var caret = state.Caret;
        var lines = state.Lines.ToList();
        var line = lines[caret.Line];
        if (caret.Offset < line.Text.Length)
        {
            return null;
        }

        if (caret.Line == lines.Count - 1)
        {
            return state;
        }

        if (line.Text.Length == 0)
        {
            lines.RemoveAt(caret.Line);
            return SurfaceState.CaretAt(lines, new SurfacePoint(caret.Line, 0));
        }

        lines[caret.Line] = line with { Text = line.Text + lines[caret.Line + 1].Text };
        lines.RemoveAt(caret.Line + 1);
        return SurfaceState.CaretAt(lines, caret);
    }

    /// <summary>
    /// Takes the selection away. Whole lines are taken whole - box and all - and the caret goes to the
    /// end of the line above them, or to the start of the one below when they were the first; that is
    /// where somebody who deleted a line expects to go on writing. A selection that starts or ends part
    /// way through a line joins what is left of the two ends into the first one.
    ///
    /// forReplacement is for an edit that goes on to write something where the selection was - typing
    /// over it, a paste, Enter. Whole lines then leave one empty line behind to write on, rather than
    /// sending the words to the end of the line above.
    /// </summary>
    public static SurfaceState DeleteSelection(SurfaceState state, bool forReplacement = false)
    {
        state = state.Normalized();
        if (state.IsCollapsed)
        {
            return state;
        }

        var (start, end) = (state.Start, state.End);
        var lines = state.Lines.ToList();
        if (CoversWholeLines(state) is { } last)
        {
            lines.RemoveRange(start.Line, last - start.Line + 1);
            if (forReplacement)
            {
                lines.Insert(start.Line, SurfaceState.EmptyLine);
                return SurfaceState.CaretAt(lines, new SurfacePoint(start.Line, 0));
            }

            return CaretAfterRemoval(lines, start.Line);
        }

        var first = lines[start.Line];
        var tail = lines[end.Line].Text[end.Offset..];
        lines.RemoveRange(start.Line + 1, end.Line - start.Line);
        lines[start.Line] = first with { Text = first.Text[..start.Offset] + tail };
        return SurfaceState.CaretAt(lines, start);
    }

    /// <summary>
    /// Writes text where the selection is. Used for typing over a selection that spans lines, which the
    /// browser would do by gluing the lines' elements together and losing their boxes, and for a paste,
    /// which the browser put at the start of the line rather than at the caret. Text with line breaks in
    /// it becomes that many lines, the caret at the end of what was written.
    ///
    /// With readsMarkers - a paste - a line of it that starts the way a typed checklist line starts
    /// ("[]", "[ ]") becomes a box, and so do the two ways a box is written out: "[x]" ticked, and the
    /// "- " bullet this surface copies one as (see onCopy in checklistTextEditor.js), so a checklist
    /// copied out and pasted back is a checklist again. Only a line the paste starts is read that way:
    /// words pasted into the middle of a line, or onto a box that is already there, are words.
    /// </summary>
    public static SurfaceState Replace(SurfaceState state, string text, bool readsMarkers)
    {
        var cleared = DeleteSelection(state.Normalized(), forReplacement: true);
        var caret = cleared.Caret;
        var lines = cleared.Lines.ToList();
        var line = lines[caret.Line];
        var before = line.Text[..caret.Offset];
        var after = line.Text[caret.Offset..];
        var written = LinesOf(text);
        var startsALine = readsMarkers && caret.Offset == 0 && !line.IsChecklistItem;

        if (written.Count == 1)
        {
            if (startsALine && Read(written[0]) is { IsChecklistItem: true } box)
            {
                lines[caret.Line] = box with { Text = box.Text + after };
                return SurfaceState.CaretAt(lines, new SurfacePoint(caret.Line, box.Text.Length));
            }

            lines[caret.Line] = line with { Text = before + written[0] + after };
            return SurfaceState.CaretAt(lines, new SurfacePoint(caret.Line, caret.Offset + written[0].Length));
        }

        var replacement = new List<NoteContentLineDto>
        {
            startsALine ? Read(written[0]) : line with { Text = before + written[0] }
        };
        replacement.AddRange(written.Skip(1).SkipLast(1).Select(pasted => readsMarkers ? Read(pasted) : Plain(pasted)));
        var last = readsMarkers ? Read(written[^1]) : Plain(written[^1]);
        replacement.Add(last with { Text = last.Text + after });

        lines.RemoveAt(caret.Line);
        lines.InsertRange(caret.Line, replacement);
        return SurfaceState.CaretAt(lines, new SurfacePoint(caret.Line + replacement.Count - 1, last.Text.Length));
    }

    /// <summary>A pasted line read the way <see cref="Replace"/> describes.</summary>
    private static NoteContentLineDto Read(string pasted)
    {
        var tick = PastedTick().Match(pasted);
        if (tick.Success)
        {
            var isTicked = tick.Groups["mark"].Value is "x" or "X";
            return new NoteContentLineDto(pasted[tick.Length..], IsChecklistItem: true, IsChecked: isTicked);
        }

        var bullet = PastedBullet().Match(pasted);
        return bullet.Success
            ? new NoteContentLineDto(pasted[bullet.Length..], IsChecklistItem: true, IsChecked: false)
            : Plain(pasted);
    }

    /// <summary>The typed marker, plus "[x]" for a box that arrives already ticked.</summary>
    [GeneratedRegex(@"^\[(?<mark>[ \txX]?)\][ \t]?")]
    private static partial Regex PastedTick();

    /// <summary>"- " as this surface copies a box out - a bare "-" too, which is an empty one.</summary>
    [GeneratedRegex(@"^-(?:[ \t]|$)")]
    private static partial Regex PastedBullet();

    /// <summary>
    /// After something was typed: a plain line that now starts "[]" (or "[ ]") becomes a tick box, and
    /// the marker is eaten. The phone's note screen has had exactly this rule and no toolbar at all -
    /// see NoteDetailPage. The caret stays where it was in the words, which is two characters further
    /// left now the marker has gone.
    /// </summary>
    public static SurfaceState? ReadTypedMarker(SurfaceState state)
    {
        state = state.Normalized();
        if (!state.IsCollapsed)
        {
            return null;
        }

        var caret = state.Caret;
        var line = state.Lines[caret.Line];
        var marker = TypedTick().Match(line.Text);
        if (line.IsChecklistItem || !marker.Success)
        {
            return null;
        }

        var lines = state.Lines.ToList();
        lines[caret.Line] = new NoteContentLineDto(line.Text[marker.Length..], IsChecklistItem: true, IsChecked: false);
        return SurfaceState.CaretAt(lines, new SurfacePoint(caret.Line, Math.Max(0, caret.Offset - marker.Length)));
    }

    /// <summary>
    /// The toolbar's tick box: an empty plain line becomes a checklist line, and anything else gets a new
    /// checklist line under it - the caret in the box's line either way, ready for its words.
    /// </summary>
    public static SurfaceState StartChecklistItem(SurfaceState state)
    {
        state = state.Normalized();
        var caret = state.Caret;
        var lines = state.Lines.ToList();
        var line = lines[caret.Line];
        var unticked = new NoteContentLineDto(string.Empty, IsChecklistItem: true, IsChecked: false);

        if (!line.IsChecklistItem && line.Text.Length == 0)
        {
            lines[caret.Line] = unticked;
            return SurfaceState.CaretAt(lines, new SurfacePoint(caret.Line, 0));
        }

        lines.Insert(caret.Line + 1, unticked);
        return SurfaceState.CaretAt(lines, new SurfacePoint(caret.Line + 1, 0));
    }

    /// <summary>
    /// One level of indentation: a tab character, not spaces. It is stored in the line's text as it is -
    /// there is no separate indentation field - so it reads the same wherever the text goes: the writing
    /// surface and the note's own page both keep whitespace (white-space: pre-wrap) and draw a tab four
    /// characters wide (tab-size), and a note copied out carries a tab any editor reads as one level.
    /// On a checklist line it is part of the words, so it indents them after the box.
    /// </summary>
    public const string Indentation = "\t";

    /// <summary>
    /// How many leading spaces Shift+Tab takes away when a line was indented with spaces rather than a
    /// tab - text pasted from elsewhere usually is.
    /// </summary>
    public const int SpacesPerIndentation = 4;

    /// <summary>
    /// Tab: a level of indentation at the caret, in place of a selection inside one line. Over a
    /// selection that spans lines, every line it covers is indented at its start instead - what a code
    /// editor does, and the only reading of "indent these" that does not throw the lines away. The lines
    /// stay selected, so a second Tab indents them again.
    /// </summary>
    public static SurfaceState Indent(SurfaceState state)
    {
        state = state.Normalized();
        if (!SpansLines(state))
        {
            return Replace(state, Indentation, readsMarkers: false);
        }

        var (first, last) = state.SelectedLines;
        var lines = state.Lines.ToList();
        for (var index = first; index <= last; index++)
        {
            lines[index] = lines[index] with { Text = Indentation + lines[index].Text };
        }

        // A point at the head of a line stays there, so the new level is inside the selection.
        SurfacePoint Shifted(SurfacePoint point)
            => point.Line >= first && point.Line <= last && point.Offset > 0
                ? point with { Offset = point.Offset + Indentation.Length }
                : point;

        return new SurfaceState(lines, Shifted(state.Anchor), Shifted(state.Focus));
    }

    /// <summary>
    /// Shift+Tab: one level less at the start of the caret's line - wherever in the line the caret is -
    /// or of every line a selection covers. A level is a tab, or up to <see cref="SpacesPerIndentation"/>
    /// spaces; a line with neither is left alone.
    /// </summary>
    public static SurfaceState Outdent(SurfaceState state)
    {
        state = state.Normalized();
        var (first, last) = state.SelectedLines;
        var lines = state.Lines.ToList();
        var removed = new int[lines.Count];
        for (var index = first; index <= last; index++)
        {
            removed[index] = LeadingIndentationLength(lines[index].Text);
            lines[index] = lines[index] with { Text = lines[index].Text[removed[index]..] };
        }

        SurfacePoint Shifted(SurfacePoint point)
            => point with { Offset = Math.Max(0, point.Offset - removed[point.Line]) };

        return new SurfaceState(lines, Shifted(state.Anchor), Shifted(state.Focus));
    }

    private static int LeadingIndentationLength(string text)
    {
        if (text.StartsWith(Indentation, StringComparison.Ordinal))
        {
            return Indentation.Length;
        }

        var spaces = 0;
        while (spaces < SpacesPerIndentation && spaces < text.Length && text[spaces] == ' ')
        {
            spaces++;
        }

        return spaces;
    }

    /// <summary>
    /// A press on a line's box: the next of its three answers (see <see cref="Ticks.Next"/>). The
    /// selection stays as it was - pressing a box is not moving the caret.
    /// </summary>
    public static SurfaceState? Cycle(SurfaceState state, int pressedLine)
    {
        state = state.Normalized();
        if (pressedLine < 0 || pressedLine >= state.Lines.Count || !state.Lines[pressedLine].IsChecklistItem)
        {
            return null;
        }

        var pressed = state.Lines[pressedLine];
        var next = Ticks.Read(pressed.IsChecked, pressed.IsFailed).Next();
        var lines = state.Lines.ToList();
        lines[pressedLine] = pressed with { IsChecked = next.IsCompleted(), IsFailed = next.IsFailed() };
        return state with { Lines = lines };
    }

    private static bool SpansLines(SurfaceState state) => state.Start.Line != state.End.Line;

    /// <summary>
    /// The last line a multi-line selection takes whole, when it takes whole lines: it starts at the head
    /// of one and ends at the head of a later one, or at the end of the last. Null for a selection that
    /// begins or ends inside words.
    /// </summary>
    private static int? CoversWholeLines(SurfaceState state)
    {
        var (start, end) = (state.Start, state.End);
        if (start.Line == end.Line || start.Offset != 0)
        {
            return null;
        }

        if (end.Offset == 0)
        {
            return end.Line - 1;
        }

        return end.Offset == state.Lines[end.Line].Text.Length ? end.Line : null;
    }

    private static SurfaceState RemoveLine(List<NoteContentLineDto> lines, int index)
    {
        lines.RemoveAt(index);
        return CaretAfterRemoval(lines, index);
    }

    /// <summary>The end of the line above what went, or the start of the first line when nothing was above it.</summary>
    private static SurfaceState CaretAfterRemoval(List<NoteContentLineDto> lines, int removedAt)
    {
        if (lines.Count == 0)
        {
            lines.Add(SurfaceState.EmptyLine);
        }

        return removedAt > 0
            ? SurfaceState.CaretAt(lines, new SurfacePoint(removedAt - 1, lines[removedAt - 1].Text.Length))
            : SurfaceState.CaretAt(lines, new SurfacePoint(0, 0));
    }

    private static IReadOnlyList<string> LinesOf(string text)
        => text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');

    private static NoteContentLineDto Plain(string text) => new(text, IsChecklistItem: false, IsChecked: false);

    private static NoteContentLineDto Unticked(NoteContentLineDto line) => line with { IsChecked = false, IsFailed = false };

    /// <summary>What typing at the head of a line turns into a box - the rule the phone has always had.</summary>
    [GeneratedRegex(@"^\[[ \t]?\][ \t]?")]
    private static partial Regex TypedTick();
}
