using System.Text.RegularExpressions;
using Orbit.Core.Abstractions;

namespace Orbit.Core.Notes;

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
    ///
    /// <b>A style follows the same two rules</b> (see <see cref="NoteLineStyle"/>, added 2026-09-14):
    /// a bulleted, dashed or numbered line carries on as one, and an empty one ends the list; a heading
    /// is one line by definition, so Enter after one starts ordinary writing. Everything else - Body,
    /// Monospaced - simply carries. That is Apple Notes' behaviour and the behaviour the tick box has
    /// had since 2026-09-12, which is why it is one rule here rather than two.
    ///
    /// With <paramref name="keepsIndentation"/> the new line starts where the line it came from starts
    /// (see <see cref="IndentationOf"/>) and the caret goes after that indentation, to the start of the
    /// line's words - so a list written with tabs stays a list when a line is added to the middle of it.
    /// The phone asks for it: its lines are one field each, and a field cannot be told to open at a
    /// column somebody has to type their way to. The browser does not, because a surface where every
    /// line is visible at once shows what it inherited and Tab is right there to change it.
    /// </summary>
    public static SurfaceState Enter(SurfaceState state, bool keepsIndentation = false)
    {
        var cleared = DeleteSelection(state.Normalized(), forReplacement: true);
        var caret = cleared.Caret;
        var lines = cleared.Lines.ToList();
        var line = lines[caret.Line];

        // A table is one line however many cells it has, and a picture one line however large: Enter on
        // either starts ordinary writing under it. For a table only the phone can send this, since the
        // browser answers Enter inside a cell itself.
        if (line.IsAnElement)
        {
            lines.Insert(caret.Line + 1, SurfaceState.EmptyLine);
            return SurfaceState.CaretAt(lines, new SurfacePoint(caret.Line + 1, 0));
        }

        // An empty line of a list ends the list, in place - a box, a bullet, a dash or a number alike.
        if (line.Text.Length == 0 && (line.IsChecklistItem || line.Style.IsAList()))
        {
            lines[caret.Line] = SurfaceState.EmptyLine;
            return SurfaceState.CaretAt(lines, new SurfacePoint(caret.Line, 0));
        }

        // At the head of a line with something on it, the new line opens above and the words stay
        // where they are - with their tick. Splitting there instead would hand the words to a fresh,
        // unticked line and leave the tick behind on an empty one. Nothing is carried down, so there
        // is nothing for indentation to be carried onto either.
        if (caret.Offset == 0 && line.Text.Length > 0)
        {
            lines.Insert(caret.Line, Continuing(line) with { Text = string.Empty });
            return SurfaceState.CaretAt(lines, new SurfacePoint(caret.Line + 1, 0));
        }

        var indentation = keepsIndentation ? IndentationOf(line.Text) : string.Empty;
        lines[caret.Line] = line.Head(caret.Offset);
        lines.Insert(caret.Line + 1, Continuing(line.Tail(caret.Offset)).Changed(0, 0, indentation));
        return SurfaceState.CaretAt(lines, new SurfacePoint(caret.Line + 1, indentation.Length));
    }

    /// <summary>
    /// The whitespace a line starts with - every tab and space of it, however many levels that is. Tabs
    /// and spaces both: a note written on a keyboard indents with one and a note written in a browser
    /// with the other, and what a line starts with is not an opinion about which of them counts.
    /// </summary>
    public static string IndentationOf(string text)
        => text[..(text.Length - text.TrimStart('\t', ' ').Length)];

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

        // Backspace on a table line is nothing: the table's own menu takes a table away, and a key
        // that could quietly delete a grid of words is not a key. The browser never sends this from
        // inside a cell - see onBeforeInput - so it is the phone's, and answered here rather than let
        // through so a field cannot go looking for something to delete.
        if (line.IsATable)
        {
            return state;
        }

        // A picture goes on Backspace, as any element in a page does - Apple Notes' rule, and the one
        // thing that tells it from a table. The bytes are swept when the note is saved without it.
        if (line.IsAPicture)
        {
            return RemoveLine(lines, caret.Line);
        }

        if (line.IsChecklistItem)
        {
            if (line.Text.Length == 0)
            {
                return RemoveLine(lines, caret.Line);
            }

            // Only the box: the words stay, with their marks, and so does what kind of line it is -
            // taking a box off a heading was never asked for and would be a second edit nobody made.
            lines[caret.Line] = line with { IsChecklistItem = false, IsChecked = false, IsFailed = false };
            return SurfaceState.CaretAt(lines, caret);
        }

        if (caret.Line == 0)
        {
            // Nothing above the first line to join. Answered rather than let through, so the browser
            // does not go looking for something outside the lines to delete.
            return state;
        }

        var previous = lines[caret.Line - 1];

        // Words cannot join a table or a picture. An empty line under one goes away and the caret lands
        // on the element, as it would on any line above; a line with words on it stays where it is.
        if (previous.IsAnElement)
        {
            if (line.Text.Length > 0)
            {
                return state;
            }

            lines.RemoveAt(caret.Line);
            return SurfaceState.CaretAt(lines, new SurfacePoint(caret.Line - 1, 0));
        }

        lines[caret.Line - 1] = previous.FollowedBy(line);
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
        if (line.IsATable)
        {
            // See Backspace: a table goes by its own menu, never by a key.
            return state;
        }

        if (line.IsAPicture)
        {
            // And a picture goes by either key, as Backspace says.
            return RemoveLine(lines, caret.Line);
        }

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

        // Words cannot take a table or a picture onto their end - the mirror of Backspace's rule.
        if (lines[caret.Line + 1].IsAnElement)
        {
            return state;
        }

        lines[caret.Line] = line.FollowedBy(lines[caret.Line + 1]);
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

        // A table at either end is kept whole. A point inside a table always reads as its head - a
        // cell is not a point on this surface - so a selection that ends on one cannot say how much of
        // it was meant, and a grid of words is not something to take on a guess. The lines between go,
        // and what is left of a text end stands beside the table on a line of its own, since words never
        // join one.
        var first = lines[start.Line];
        var lastLine = lines[end.Line];
        var head = first.IsAnElement ? null : first.Head(start.Offset);
        var tail = lastLine.IsAnElement ? null : lastLine.Tail(end.Offset);
        var kept = new List<NoteContentLine>();
        if (first.IsAnElement)
        {
            kept.Add(first);
        }

        if (head is not null || tail is not null)
        {
            kept.Add(Joined(head, tail));
        }

        if (lastLine.IsAnElement && end.Line != start.Line)
        {
            kept.Add(lastLine);
        }

        lines.RemoveRange(start.Line, end.Line - start.Line + 1);
        lines.InsertRange(start.Line, kept);
        var caretLine = start.Line + (first.IsAnElement ? 1 : 0);
        return SurfaceState.CaretAt(lines, new SurfacePoint(Math.Min(caretLine, lines.Count - 1), head?.Text.Length ?? 0));
    }

    /// <summary>What is left of a selection's two ends as one line - either, both joined, or an empty line when neither had anything to keep.</summary>
    private static NoteContentLine Joined(NoteContentLine? head, NoteContentLine? tail)
    {
        if (head is not null && tail is not null)
        {
            return head.FollowedBy(tail);
        }

        return head ?? tail ?? SurfaceState.EmptyLine;
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
        var written = LinesOf(text);

        // Writing that lands on a table or a picture goes under it, as lines of its own: neither has a
        // point in it for words to go in at, and a table's cells are the browser's to type in.
        if (line.IsAnElement)
        {
            var under = written.Select(pasted => readsMarkers ? Read(pasted) : Plain(pasted)).ToList();
            lines.InsertRange(caret.Line + 1, under);
            return SurfaceState.CaretAt(lines, new SurfacePoint(caret.Line + under.Count, under[^1].Text.Length));
        }

        var head = line.Head(caret.Offset);
        var tail = line.Tail(caret.Offset);
        var startsALine = readsMarkers && caret.Offset == 0 && !line.IsChecklistItem;

        if (written.Count == 1)
        {
            if (startsALine && Read(written[0]) is { IsChecklistItem: true } box)
            {
                lines[caret.Line] = box.FollowedBy(tail);
                return SurfaceState.CaretAt(lines, new SurfacePoint(caret.Line, box.Text.Length));
            }

            lines[caret.Line] = line.Changed(caret.Offset, 0, written[0]);
            return SurfaceState.CaretAt(lines, new SurfacePoint(caret.Line, caret.Offset + written[0].Length));
        }

        var replacement = new List<NoteContentLine>
        {
            startsALine ? Read(written[0]) : head.Changed(caret.Offset, 0, written[0])
        };
        replacement.AddRange(written.Skip(1).SkipLast(1).Select(pasted => readsMarkers ? Read(pasted) : Plain(pasted)));
        var last = readsMarkers ? Read(written[^1]) : Plain(written[^1]);
        replacement.Add(last.FollowedBy(tail));

        lines.RemoveAt(caret.Line);
        lines.InsertRange(caret.Line, replacement);
        return SurfaceState.CaretAt(lines, new SurfacePoint(caret.Line + replacement.Count - 1, last.Text.Length));
    }

    /// <summary>A pasted line read the way <see cref="Replace"/> describes.</summary>
    private static NoteContentLine Read(string pasted)
    {
        var tick = PastedTick().Match(pasted);
        if (tick.Success)
        {
            var isTicked = tick.Groups["mark"].Value is "x" or "X";
            return new NoteContentLine(pasted[tick.Length..], IsChecklistItem: true, IsChecked: isTicked);
        }

        var bullet = PastedBullet().Match(pasted);
        return bullet.Success
            ? new NoteContentLine(pasted[bullet.Length..], IsChecklistItem: true, IsChecked: false)
            : Plain(pasted);
    }

    /// <summary>The typed marker, plus "[x]" for a box that arrives already ticked.</summary>
    [GeneratedRegex(@"^\[(?<mark>[ \txX]?)\][ \t]?")]
    private static partial Regex PastedTick();

    /// <summary>"- " as this surface copies a box out - a bare "-" too, which is an empty one.</summary>
    [GeneratedRegex(@"^-(?:[ \t]|$)")]
    private static partial Regex PastedBullet();

    /// <summary>
    /// One line of pasted text read the way <see cref="Replace"/> reads a line a paste starts: "[]",
    /// "[ ]" and "- " make a box, "[x]" a ticked one, anything else is words. For the phone, whose fields
    /// report only the text a paste left behind rather than the paste itself - see NoteDetailViewModel.
    /// </summary>
    public static NoteContentLine ReadPastedLine(string pasted) => Read(pasted);

    /// <summary>
    /// How many characters of <paramref name="text"/> are the typed checklist mark it starts with - "[]"
    /// or "[ ]" and one space after either, the rule <see cref="ReadTypedMarker"/> follows - or 0 when it
    /// starts with none. For the phone, which reads the mark after a line's indentation.
    /// </summary>
    public static int TypedMarkerLength(string text)
    {
        var marker = TypedTick().Match(text);
        return marker.Success ? marker.Length : 0;
    }

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
        if (line.IsChecklistItem || line.IsAnElement || !marker.Success)
        {
            return null;
        }

        var lines = state.Lines.ToList();
        // The line's style is kept: a box is what the line is answered in, not what kind of line it is -
        // see NoteLineStyle, which says why the two are separate fields.
        lines[caret.Line] = line.Changed(0, marker.Length, string.Empty) with
        {
            IsChecklistItem = true,
            IsChecked = false,
            IsFailed = false
        };
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
        var unticked = new NoteContentLine(string.Empty, IsChecklistItem: true, IsChecked: false);

        if (!line.IsChecklistItem && !line.IsAnElement && line.Text.Length == 0)
        {
            // In place, so the line keeps what it is - an empty line of a list given a box is still a
            // line of that list. The line started below is a new one and starts as ordinary writing.
            lines[caret.Line] = unticked with { Style = line.Style };
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
            // A table has no head to put a level at; the key does nothing there rather than writing
            // a tab under it.
            return state.Lines[state.Caret.Line].IsAnElement ? state : Replace(state, Indentation, readsMarkers: false);
        }

        var (first, last) = state.SelectedLines;
        var lines = state.Lines.ToList();
        for (var index = first; index <= last; index++)
        {
            if (!lines[index].IsAnElement)
            {
                lines[index] = lines[index].Changed(0, 0, Indentation);
            }
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
            if (lines[index].IsAnElement)
            {
                continue;
            }

            removed[index] = LeadingIndentationLength(lines[index].Text);
            lines[index] = lines[index].Changed(0, removed[index], string.Empty);
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
    /// A press on a line's box: the next of its three answers (see <see cref="Ticks.Next"/>). When the
    /// line is one of several checklist lines inside the selection (see
    /// <see cref="SelectedChecklistLines"/>), every one of them takes that same answer - the pressed line
    /// decides, so a mixed set ends up all alike rather than each stepping on from where it was. A box
    /// outside the selection answers only for itself. The selection stays as it was - pressing a box is
    /// not moving the caret - so a second press carries on with the same lines.
    /// </summary>
    public static SurfaceState? Cycle(SurfaceState state, int pressedLine)
    {
        state = state.Normalized();
        return Cycle(state.Lines, pressedLine, SelectedChecklistLines(state)) is { } lines
            ? state with { Lines = lines }
            : null;
    }

    /// <summary>
    /// The same press, for boxes chosen some other way than by a selection of text - the phone marks
    /// them one at a time, and they need not stand next to each other. <paramref name="together"/> are
    /// the chosen lines: when the pressed line is one of two or more chosen boxes, every chosen box takes
    /// the pressed box's next answer, and otherwise the pressed box answers alone. Null when the pressed
    /// line has no box.
    /// </summary>
    public static IReadOnlyList<NoteContentLine>? Cycle(
        IReadOnlyList<NoteContentLine> lines, int pressedLine, IReadOnlyCollection<int> together)
    {
        if (pressedLine < 0 || pressedLine >= lines.Count || !lines[pressedLine].IsChecklistItem)
        {
            return null;
        }

        var pressed = lines[pressedLine];
        var next = Ticks.Read(pressed.IsChecked, pressed.IsFailed).Next();
        var chosen = together.Where(index => index >= 0 && index < lines.Count && lines[index].IsChecklistItem).ToList();
        IEnumerable<int> answering = chosen.Count >= 2 && chosen.Contains(pressedLine) ? chosen : [pressedLine];

        var result = lines.ToList();
        foreach (var index in answering)
        {
            result[index] = result[index] with { IsChecked = next.IsCompleted(), IsFailed = next.IsFailed() };
        }

        return result;
    }

    /// <summary>
    /// The checklist lines a selection covers, when there are at least two of them - which is when a
    /// press on one of their boxes answers for all of them, and when the surface rings those boxes to
    /// say so. One box on its own is just a line with the caret in it, and a collapsed caret selects
    /// nothing.
    /// </summary>
    public static IReadOnlyList<int> SelectedChecklistLines(SurfaceState state)
    {
        state = state.Normalized();
        if (state.IsCollapsed)
        {
            return [];
        }

        var (first, last) = state.SelectedLines;
        var checklist = Enumerable.Range(first, last - first + 1)
            .Where(index => state.Lines[index].IsChecklistItem)
            .ToList();
        return checklist.Count >= 2 ? checklist : [];
    }

    /// <summary>
    /// A drag of the selection, dropped at <paramref name="to"/>: the selected writing is taken away and
    /// put in there - or, with <paramref name="copies"/>, put in there and left where it was too - as one
    /// edit, so one Ctrl+Z puts it all back. The browser's own drag glued two lines' elements together
    /// when the selection spanned lines; this keeps every line a line.
    ///
    /// Whole lines (the rule <see cref="DeleteSelection"/> takes whole) go whole, box and tick and all,
    /// and land between lines: before the line dropped on when the drop is at its head, after it
    /// otherwise - a line dropped into the middle of a sentence has no better place to go. Anything
    /// else goes in at the point as writing does: the first line joins the words before the point and
    /// the words after the point join the last. A line of the selection keeps its box when its head was
    /// selected. The moved writing is selected afterwards, which is what the browser does with a drop.
    ///
    /// Null when there is nothing to move, or the drop is inside the selection itself - there is
    /// nowhere for it to go.
    /// </summary>
    public static SurfaceState? Drag(SurfaceState state, SurfacePoint to, bool copies)
    {
        state = state.Normalized();
        to = SurfaceState.CaretAt(state.Lines, to).Normalized().Caret;
        var (start, end) = (state.Start, state.End);
        if (state.IsCollapsed || (to.CompareTo(start) >= 0 && to.CompareTo(end) <= 0))
        {
            return null;
        }

        var wholeLinesTo = CoversWholeLines(state);
        var moved = wholeLinesTo is { } last
            ? state.Lines.Skip(start.Line).Take(last - start.Line + 1).ToList()
            : SelectedFragment(state);

        var lines = copies ? state.Lines.ToList() : DeleteSelection(state).Lines.ToList();
        var at = copies ? to : WhereAfterRemoval(state, to, wholeLinesTo);
        return wholeLinesTo is null ? InsertFragment(lines, at, moved) : InsertLines(lines, at, moved);
    }

    /// <summary>
    /// Text dragged in from somewhere else - another page, another program - dropped at
    /// <paramref name="to"/>. Read the way a paste is (see <see cref="Replace"/>), and selected afterwards
    /// the way a drop is.
    /// </summary>
    public static SurfaceState Drop(SurfaceState state, SurfacePoint to, string text, bool readsMarkers)
    {
        var at = SurfaceState.CaretAt(state.Normalized().Lines, to).Normalized();
        var after = Replace(at, text, readsMarkers);
        return new SurfaceState(after.Lines, at.Caret, after.Caret);
    }

    /// <summary>
    /// The selection as lines of its own, for a selection that does not take whole lines: the tail of the
    /// first line, every line between, and the head of the last. The first keeps its box only when it was
    /// selected from its head; the others always were. A selection ending at the head of a line brings
    /// the line break and nothing of that line.
    /// </summary>
    private static List<NoteContentLine> SelectedFragment(SurfaceState state)
    {
        var (start, end) = (state.Start, state.End);
        var first = state.Lines[start.Line];
        if (start.Line == end.Line)
        {
            return [Words(first, start.Offset, end.Offset - start.Offset)];
        }

        var fragment = new List<NoteContentLine>
        {
            start.Offset == 0 ? first : Words(first, start.Offset, first.Text.Length - start.Offset)
        };
        fragment.AddRange(state.Lines.Skip(start.Line + 1).Take(end.Line - start.Line - 1));
        var lastLine = state.Lines[end.Line];
        fragment.Add(end.Offset == 0 ? SurfaceState.EmptyLine : lastLine.Head(end.Offset));
        return fragment;
    }

    /// <summary>
    /// Where a point after the selection stands once <see cref="DeleteSelection"/> has taken the selection
    /// away. A point before it does not move.
    /// </summary>
    private static SurfacePoint WhereAfterRemoval(SurfaceState state, SurfacePoint point, int? wholeLinesTo)
    {
        var (start, end) = (state.Start, state.End);
        if (point.CompareTo(start) < 0)
        {
            return point;
        }

        if (wholeLinesTo is { } last)
        {
            return point with { Line = point.Line - (last - start.Line + 1) };
        }

        return point.Line == end.Line
            ? new SurfacePoint(start.Line, start.Offset + point.Offset - end.Offset)
            : point with { Line = point.Line - (end.Line - start.Line) };
    }

    /// <summary>Whole lines put in between lines - see <see cref="Drag"/> for which side of the line dropped on.</summary>
    private static SurfaceState InsertLines(List<NoteContentLine> lines, SurfacePoint at, List<NoteContentLine> moved)
    {
        var index = at.Offset == 0 ? at.Line : at.Line + 1;
        lines.InsertRange(index, moved);
        return new SurfaceState(lines, new SurfacePoint(index, 0), new SurfacePoint(index + moved.Count - 1, moved[^1].Text.Length));
    }

    /// <summary>
    /// Part-lines put in at a point as writing is. At the head of a plain line the first of them brings
    /// its own box; anywhere else it joins the line it lands in and takes that line's box.
    /// </summary>
    private static SurfaceState InsertFragment(List<NoteContentLine> lines, SurfacePoint at, List<NoteContentLine> moved)
    {
        var line = lines[at.Line];
        if (line.IsAnElement)
        {
            // Part-lines cannot join a table or a picture any more than whole ones can; they go under
            // it as lines of their own, the way a paste that lands on one does.
            lines.InsertRange(at.Line + 1, moved);
            return new SurfaceState(
                lines, new SurfacePoint(at.Line + 1, 0), new SurfacePoint(at.Line + moved.Count, moved[^1].Text.Length));
        }

        var head = line.Head(at.Offset);
        var tail = line.Tail(at.Offset);
        if (moved.Count == 1)
        {
            lines[at.Line] = head.FollowedBy(moved[0]).FollowedBy(tail);
            return new SurfaceState(lines, at, at with { Offset = at.Offset + moved[0].Text.Length });
        }

        var replacement = new List<NoteContentLine>
        {
            at.Offset == 0 && !line.IsChecklistItem ? moved[0] : head.FollowedBy(moved[0])
        };
        replacement.AddRange(moved.Skip(1).SkipLast(1));
        replacement.Add(moved[^1].FollowedBy(tail));

        lines.RemoveAt(at.Line);
        lines.InsertRange(at.Line, replacement);
        return new SurfaceState(lines, at, new SurfacePoint(at.Line + moved.Count - 1, moved[^1].Text.Length));
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

    private static SurfaceState RemoveLine(List<NoteContentLine> lines, int index)
    {
        lines.RemoveAt(index);
        return CaretAfterRemoval(lines, index);
    }

    /// <summary>The end of the line above what went, or the start of the first line when nothing was above it.</summary>
    private static SurfaceState CaretAfterRemoval(List<NoteContentLine> lines, int removedAt)
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

    private static NoteContentLine Plain(string text) => new(text, IsChecklistItem: false, IsChecked: false);

    /// <summary>
    /// A stretch of a line's words as a fragment of its own: the words and the marks on them, and none of
    /// what the line was. A fragment carries writing - a box and a heading belong to the line it was cut
    /// out of, not to the words.
    /// </summary>
    private static NoteContentLine Words(NoteContentLine line, int start, int length)
        => Plain(line.Text.Substring(start, length)) with
        {
            Marks = NoteTextMarks.Taken(line.AllMarks, start, length)
        };

    private static NoteContentLine Unticked(NoteContentLine line) => line with { IsChecked = false, IsFailed = false };

    /// <summary>
    /// The line Enter starts after <paramref name="line"/>: the same kind of line, unticked, except that
    /// a heading is one line and is followed by ordinary writing. What carries is the shape, never the
    /// answer - a new box is empty, and a new numbered line takes its number from where it lands (see
    /// NoteLineStyles.NumberOf) rather than from the line it came from.
    /// </summary>
    private static NoteContentLine Continuing(NoteContentLine line)
        => Unticked(line) with { Style = line.Style.IsAHeading() ? NoteLineStyle.Body : line.Style };

    /// <summary>
    /// Makes every line the caret or the selection touches this style, or takes it back to
    /// <see cref="NoteLineStyle.Body"/> where they are all already it - the way a format control works
    /// everywhere: pressing what a line already is turns it off.
    ///
    /// A tick box is not a style and is left exactly as it is (see <see cref="NoteLineStyle"/>): a
    /// checklist line made into a heading is a heading with a box on it, which is what asking for both
    /// means. Nothing about the words changes, so the caret stays where it is - this is the one edit on
    /// this surface that moves no text at all.
    /// </summary>
    public static SurfaceState Restyle(SurfaceState state, NoteLineStyle style)
    {
        state = state.Normalized();
        var (first, last) = state.SelectedLines;
        var lines = state.Lines.ToList();
        var alreadyAllOfIt = Enumerable.Range(first, last - first + 1)
            .All(index => lines[index].IsAnElement || lines[index].Style == style);
        var wanted = alreadyAllOfIt ? NoteLineStyle.Body : style;

        for (var index = first; index <= last; index++)
        {
            // A table is words in a grid and a picture is not words at all: neither has a style.
            if (!lines[index].IsAnElement)
            {
                lines[index] = lines[index] with { Style = wanted };
            }
        }

        return new SurfaceState(lines, state.Anchor, state.Focus);
    }

    /// <summary>
    /// Puts a mark on the words the selection covers - bold, italic, underlined, struck through - or
    /// takes it off where every one of them already carries it. The rule a style follows, applied to a
    /// stretch of words instead of to whole lines: pressing what something already is turns it off.
    ///
    /// <b>A caret with nothing selected does nothing.</b> There are no words to mark, and a control that
    /// answered such a press would have to remember that the next thing typed is bold - which is the
    /// browser's business, since it is the browser that carries the caret and draws what is typed.
    ///
    /// On or off is decided once, over the whole selection, and then said to each line: a selection half
    /// bold is one somebody is asking to make bold, not one they are asking to turn off - and a selection
    /// that flipped line by line would come back striped.
    /// </summary>
    public static SurfaceState Mark(SurfaceState state, NoteTextMark mark)
    {
        state = state.Normalized();
        if (state.IsCollapsed)
        {
            return state;
        }

        var (start, end) = (state.Start, state.End);
        var lines = state.Lines.ToList();
        var alreadyAllOfIt = Enumerable.Range(start.Line, end.Line - start.Line + 1).All(index =>
        {
            var (from, length) = Selected(lines[index], index, start, end);
            return length == 0 || NoteTextMarks.Holds(lines[index].AllMarks, from, length, mark);
        });

        for (var index = start.Line; index <= end.Line; index++)
        {
            var line = lines[index];
            var (from, length) = Selected(line, index, start, end);
            if (length == 0)
            {
                continue;
            }

            lines[index] = line with
            {
                Marks = alreadyAllOfIt
                    ? NoteTextMarks.Without(line.AllMarks, from, length, mark, line.Text.Length)
                    : NoteTextMarks.With(line.AllMarks, from, length, mark, line.Text.Length)
            };
        }

        return new SurfaceState(lines, state.Anchor, state.Focus);
    }

    /// <summary>
    /// Which of a line's characters a selection covers: from the caret on the line it starts on, to the
    /// caret on the line it ends on, and the whole of every line in between.
    /// </summary>
    private static (int From, int Length) Selected(
        NoteContentLine line, int index, SurfacePoint start, SurfacePoint end)
    {
        var from = index == start.Line ? start.Offset : 0;
        var to = index == end.Line ? end.Offset : line.Text.Length;
        return (from, Math.Max(0, to - from));
    }

    /// <summary>
    /// Whether every word the selection covers already carries <paramref name="mark"/> - what the control
    /// over the writing draws itself by, so Bold is lit while the caret is in bold words.
    /// </summary>
    public static bool Holds(SurfaceState state, NoteTextMark mark)
    {
        state = state.Normalized();
        if (state.IsCollapsed)
        {
            return false;
        }

        var (start, end) = (state.Start, state.End);
        var anything = false;
        for (var index = start.Line; index <= end.Line; index++)
        {
            var line = state.Lines[index];
            var (from, length) = Selected(line, index, start, end);
            if (length == 0)
            {
                continue;
            }

            anything = true;
            if (!NoteTextMarks.Holds(line.AllMarks, from, length, mark))
            {
                return false;
            }
        }

        return anything;
    }

    /// <summary>
    /// The table tool: an empty plain line becomes a table, and anything else gets a table under it -
    /// the rule the tick-box tool follows, for the same reason (see <see cref="StartChecklistItem"/>).
    /// A selection is written over first, as every insertion writes over one. The caret is put on the
    /// table's line; which cell it lands in is the browser's to decide, since a cell is not a point on
    /// this surface.
    /// </summary>
    public static SurfaceState InsertTable(SurfaceState state)
    {
        var cleared = DeleteSelection(state.Normalized(), forReplacement: true);
        var caret = cleared.Caret;
        var lines = cleared.Lines.ToList();
        var line = lines[caret.Line];
        return Placed(lines, caret, NoteContentLine.OfTable(NoteTables.Empty()));
    }

    /// <summary>
    /// A picture pasted or dropped into the note: an empty plain line becomes it, anything else gets it
    /// underneath - the rule the table tool and the tick-box tool follow. The picture is already in the
    /// store by now; this is only where it stands in the writing.
    /// </summary>
    public static SurfaceState InsertPicture(SurfaceState state, NotePictureLine picture)
    {
        var cleared = DeleteSelection(state.Normalized(), forReplacement: true);
        return Placed(cleared.Lines.ToList(), cleared.Caret, NoteContentLine.OfPicture(picture));
    }

    /// <summary>An element put where the caret is: in place of an empty plain line, or under any other. The caret goes to it.</summary>
    private static SurfaceState Placed(List<NoteContentLine> lines, SurfacePoint caret, NoteContentLine element)
    {
        var line = lines[caret.Line];
        if (!line.IsChecklistItem && !line.IsAnElement && line.Text.Length == 0)
        {
            lines[caret.Line] = element;
            return SurfaceState.CaretAt(lines, new SurfacePoint(caret.Line, 0));
        }

        lines.Insert(caret.Line + 1, element);
        return SurfaceState.CaretAt(lines, new SurfacePoint(caret.Line + 1, 0));
    }

    /// <summary>A new empty row under row <paramref name="afterRow"/> of the table on line <paramref name="line"/>.</summary>
    public static SurfaceState? AddTableRow(SurfaceState state, int line, int afterRow)
        => Reshaped(state, line, table => NoteTables.WithRowAfter(table, afterRow));

    /// <summary>A new empty column to the right of column <paramref name="afterColumn"/>.</summary>
    public static SurfaceState? AddTableColumn(SurfaceState state, int line, int afterColumn)
        => Reshaped(state, line, table => NoteTables.WithColumnAfter(table, afterColumn));

    /// <summary>Row <paramref name="row"/> taken away. The last row taken away takes the table with it - see <see cref="Reshaped"/>.</summary>
    public static SurfaceState? RemoveTableRow(SurfaceState state, int line, int row)
        => Reshaped(state, line, table => NoteTables.WithoutRow(table, row));

    /// <summary>Column <paramref name="column"/> taken away, the last one taking the table with it.</summary>
    public static SurfaceState? RemoveTableColumn(SurfaceState state, int line, int column)
        => Reshaped(state, line, table => NoteTables.WithoutColumn(table, column));

    /// <summary>The whole table taken away, and an empty line left where it stood to write on.</summary>
    public static SurfaceState? RemoveTable(SurfaceState state, int line)
        => Reshaped(state, line, _ => null);

    /// <summary>
    /// One change of shape to the table on <paramref name="line"/>, or null when that line is not a
    /// table - a stale press, answered by doing nothing. A table that <paramref name="reshape"/> answers
    /// null for is gone, and the line becomes an empty line of writing rather than vanishing: what
    /// stood there was the reader's, and a line to write on is where they were.
    /// </summary>
    private static SurfaceState? Reshaped(SurfaceState state, int line, Func<NoteTable, NoteTable?> reshape)
    {
        state = state.Normalized();
        if (line < 0 || line >= state.Lines.Count || state.Lines[line].Table is not { } table)
        {
            return null;
        }

        var lines = state.Lines.ToList();
        lines[line] = reshape(table) is { } reshaped ? NoteContentLine.OfTable(reshaped) : SurfaceState.EmptyLine;
        return SurfaceState.CaretAt(lines, new SurfacePoint(line, 0));
    }

    /// <summary>What typing at the head of a line turns into a box - the rule the phone has always had.</summary>
    [GeneratedRegex(@"^\[[ \t]?\][ \t]?")]
    private static partial Regex TypedTick();
}
