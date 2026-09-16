using System.Text.Json;
using System.Text.Json.Serialization;

namespace Orbit.Core.Notes;

/// <summary>
/// A mark on a stretch of words inside one line - what Apple Notes' Format menu offers under the styles,
/// and what <see cref="NoteLineStyle"/> deliberately cannot say: a style is the whole line's, this is
/// part of one's.
///
/// Four of them, and no colour among them: a note is the reader's writing, and a word that arrived in the
/// app's accent would be Orbit having an opinion about their words - the same rule the styles follow.
/// </summary>
[JsonConverter(typeof(NoteTextMarkJsonConverter))]
public enum NoteTextMark
{
    /// <summary>
    /// No mark, and nothing drawn for one. Not offered anywhere: it is what a mark written by a build
    /// newer than this one reads as, so a note saved with a mark this build has never heard of still
    /// opens - with those words drawn plainly - rather than refusing to load. Dropped by
    /// <see cref="NoteTextMarks.Normalized"/>, so it never survives an edit either.
    /// </summary>
    None,

    Bold,

    Italic,

    Underlined,

    /// <summary>
    /// Struck through - words the writer kept and finished with. Nothing to do with a checklist line's
    /// cross (<see cref="NoteContentLine.IsFailed"/>), which is about the line's answer rather than its
    /// words, and which strikes the whole line rather than a stretch of it.
    /// </summary>
    StruckThrough
}

/// <summary>
/// One mark over one stretch of a line's characters: from <paramref name="Start"/>, running
/// <paramref name="Length"/> characters. Half-open, as every offset on this surface is - see
/// SurfacePoint, whose offsets these are counted the same way.
///
/// One mark each rather than a set, so two marks over the same words are two of these. That keeps every
/// rule below about one mark at a time - which is what makes splitting, joining and shifting them
/// arithmetic rather than bookkeeping - and it costs nothing, since a line carries a handful at most.
/// </summary>
public sealed record NoteTextRun(int Start, int Length, NoteTextMark Mark)
{
    /// <summary>One past the last character this covers.</summary>
    public int End => Start + Length;
}

/// <summary>
/// What happens to a line's marks when its words change. Every rule here is the same question asked of a
/// different edit: a stretch of the line was cut, added to, or carried somewhere else, so where does each
/// mark end up?
///
/// Here rather than in either client, for the reason every other rule about a note's surface is: the
/// browser and the phone must agree about what a note says, and two implementations of "the words moved
/// four to the right" are two answers waiting to differ.
///
/// <b>A mark sticks to the character before what arrives.</b> Text put inside a bold stretch, or right
/// after one, is bold; text put at the very head of one is not. That is what every editor does, and it is
/// what the browser will report having drawn anyway - so the rules here answer the same way rather than a
/// second way. The edits these are for are the ones C# owns: a line split, two lines joined, a paste, a
/// level of indentation, a "[]" taken back out.
/// </summary>
public static class NoteTextMarks
{
    /// <summary>Nothing marked - what a line carries unless it says otherwise.</summary>
    public static readonly IReadOnlyList<NoteTextRun> None = [];

    /// <summary>
    /// The text cut into the longest stretches that carry the same marks. What drawing needs and what a
    /// list of marks does not say directly, since two marks over the same words are two runs of their
    /// own: a reader has to be handed "these words, bold and italic" rather than two overlapping claims.
    ///
    /// Every stretch names its marks in one order always, so the same set reads as the same stretch.
    /// Answers nothing at all for empty text.
    /// </summary>
    public static IReadOnlyList<NoteTextPiece> Pieces(string text, IEnumerable<NoteTextRun> marks)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        var carried = new List<NoteTextMark>[text.Length];
        for (var at = 0; at < text.Length; at++)
        {
            carried[at] = [];
        }

        foreach (var run in Normalized(marks, text.Length))
        {
            for (var at = run.Start; at < run.End; at++)
            {
                carried[at].Add(run.Mark);
            }
        }

        var pieces = new List<NoteTextPiece>();
        var from = 0;
        for (var at = 1; at <= text.Length; at++)
        {
            if (at < text.Length && carried[at].SequenceEqual(carried[from]))
            {
                continue;
            }

            pieces.Add(new NoteTextPiece(text[from..at], carried[from]));
            from = at;
        }

        return pieces;
    }

    /// <summary>
    /// A mark read off the word it is sent and stored as. One this build does not know reads as
    /// <see cref="NoteTextMark.None"/>, which draws nothing and is dropped by
    /// <see cref="Normalized"/> - the rule every stored-by-name enum in Orbit follows, and the reason a
    /// note saved by a newer build still opens here.
    /// </summary>
    public static NoteTextMark Read(string? mark)
        => Enum.TryParse<NoteTextMark>(mark, ignoreCase: true, out var known) ? known : NoteTextMark.None;

    /// <summary>
    /// The marks in one settled shape: nothing empty, nothing running past the words, nothing carrying a
    /// mark this build does not know, and touching or overlapping stretches of the same mark made one.
    /// Everything below answers through this, so a line's marks are only ever in this shape - which is
    /// what lets two lines be compared by what they say rather than by how they were built.
    /// </summary>
    public static IReadOnlyList<NoteTextRun> Normalized(IEnumerable<NoteTextRun> runs, int textLength)
    {
        var clipped = runs
            .Where(run => run.Mark != NoteTextMark.None)
            .Select(run => (Start: Math.Max(0, run.Start), End: Math.Min(textLength, run.End), run.Mark))
            .Where(stretch => stretch.End > stretch.Start)
            .OrderBy(stretch => stretch.Mark)
            .ThenBy(stretch => stretch.Start)
            .ToList();

        var joined = new List<NoteTextRun>();
        foreach (var stretch in clipped)
        {
            if (joined.Count > 0 && joined[^1].Mark == stretch.Mark && joined[^1].End >= stretch.Start)
            {
                var end = Math.Max(joined[^1].End, stretch.End);
                joined[^1] = joined[^1] with { Length = end - joined[^1].Start };
                continue;
            }

            joined.Add(new NoteTextRun(stretch.Start, stretch.End - stretch.Start, stretch.Mark));
        }

        return joined;
    }

    /// <summary>
    /// The marks after <paramref name="removed"/> characters were taken out at <paramref name="at"/> and
    /// <paramref name="inserted"/> put in their place. A mark over words that are gone shrinks; a mark
    /// wholly inside them goes with them; everything after moves by the difference.
    /// </summary>
    /// <param name="textLength">How long the line is <em>after</em> the change - what the marks are clipped to.</param>
    public static IReadOnlyList<NoteTextRun> Kept(
        IEnumerable<NoteTextRun> runs, int at, int removed, int inserted, int textLength)
    {
        var cut = at + removed;
        var moved = inserted - removed;

        return Normalized(
            runs.Select(run =>
            {
                var start = run.Start >= cut ? run.Start + moved : Math.Min(run.Start, at);
                var end = run.End >= cut ? run.End + moved : Math.Min(run.End, at);
                return run with { Start = start, Length = end - start };
            }),
            textLength);
    }

    /// <summary>The marks over the first <paramref name="at"/> characters - the half a split leaves behind.</summary>
    public static IReadOnlyList<NoteTextRun> Before(IEnumerable<NoteTextRun> runs, int at)
        => Normalized(runs, at);

    /// <summary>
    /// The marks over everything from <paramref name="at"/> on, counted from the start of what is left -
    /// the half a split carries down to the new line.
    /// </summary>
    public static IReadOnlyList<NoteTextRun> After(IEnumerable<NoteTextRun> runs, int at, int textLength)
        => Taken(runs, at, textLength - at);

    /// <summary>
    /// The marks over <paramref name="length"/> characters from <paramref name="start"/>, counted from
    /// the start of that stretch - what a copy, a cut or a drag carries with the words.
    /// </summary>
    public static IReadOnlyList<NoteTextRun> Taken(IEnumerable<NoteTextRun> runs, int start, int length)
        => Normalized(
            runs.Select(run =>
            {
                var from = Math.Max(run.Start, start);
                var to = Math.Min(run.End, start + length);
                return run with { Start = from - start, Length = to - from };
            }),
            Math.Max(0, length));

    /// <summary>
    /// Two lines' marks as one line's, after the second was joined onto the end of the first - which is
    /// what Backspace at the head of a line does. The marks of what came second move along by however
    /// much was already there, and a mark that ran to the end of the first does <em>not</em> spread over
    /// the second: the words were marked, not the gap between them.
    /// </summary>
    public static IReadOnlyList<NoteTextRun> Joined(
        IEnumerable<NoteTextRun> first, int firstLength, IEnumerable<NoteTextRun> second, int secondLength)
        => Normalized(
            first.Concat(second.Select(run => run with { Start = run.Start + firstLength })),
            firstLength + secondLength);

    /// <summary>
    /// Whether every character of a stretch already carries <paramref name="mark"/> - which is what the
    /// control over the writing draws itself by, and what decides whether pressing it puts the mark on or
    /// takes it off. A stretch with nothing in it carries nothing, so a caret with no selection reads as
    /// off.
    /// </summary>
    public static bool Holds(IEnumerable<NoteTextRun> runs, int start, int length, NoteTextMark mark)
        => length > 0
            && Normalized(runs.Where(run => run.Mark == mark), start + length)
                .Any(run => run.Start <= start && run.End >= start + length);

    /// <summary>
    /// The marks after the control was pressed over a stretch: the mark goes on, or comes off where every
    /// character of the stretch already had it. The same rule a style follows - pressing what something
    /// already is turns it off - and the rule every format control anywhere follows.
    ///
    /// For one line. A selection over several decides on or off once, across all of them, and then says
    /// which - see <see cref="With"/> and <see cref="Without"/>, and NoteSurfaceEdits.Mark, which is what
    /// the control actually calls.
    /// </summary>
    public static IReadOnlyList<NoteTextRun> Marked(
        IEnumerable<NoteTextRun> runs, int start, int length, NoteTextMark mark, int textLength)
    {
        var all = runs.ToList();
        return Holds(all, start, length, mark)
            ? Without(all, start, length, mark, textLength)
            : With(all, start, length, mark, textLength);
    }

    /// <summary>The mark put on a stretch, whatever it already carried.</summary>
    public static IReadOnlyList<NoteTextRun> With(
        IEnumerable<NoteTextRun> runs, int start, int length, NoteTextMark mark, int textLength)
    {
        var all = runs.ToList();
        return length <= 0 || mark == NoteTextMark.None
            ? Normalized(all, textLength)
            : Normalized([.. all, new NoteTextRun(start, length, mark)], textLength);
    }

    /// <summary>
    /// The mark taken off a stretch. Each stretch of it is cut back around those words, and the other
    /// marks are left exactly as they are - pressing Bold says nothing about what is italic.
    /// </summary>
    public static IReadOnlyList<NoteTextRun> Without(
        IEnumerable<NoteTextRun> runs, int start, int length, NoteTextMark mark, int textLength)
    {
        var all = runs.ToList();
        if (length <= 0 || mark == NoteTextMark.None)
        {
            return Normalized(all, textLength);
        }

        var kept = all.Where(run => run.Mark != mark).ToList();
        foreach (var run in all.Where(run => run.Mark == mark))
        {
            var headEnd = Math.Min(run.End, start);
            kept.Add(run with { Length = headEnd - run.Start });

            var tailStart = Math.Max(run.Start, start + length);
            kept.Add(run with { Start = tailStart, Length = run.End - tailStart });
        }

        return Normalized(kept, textLength);
    }
}

/// <summary>
/// A stretch of a line's words and every mark on all of it - what something drawing a line is handed, one
/// piece after another. See <see cref="NoteTextMarks.Pieces"/>.
/// </summary>
public sealed record NoteTextPiece(string Text, IReadOnlyList<NoteTextMark> Marks);

/// <summary>
/// Writes a mark by its name and reads one back the same way, for the reason
/// <see cref="NoteLineStyleJsonConverter"/> does: a note's content is stored as JSON, so a number there
/// would mean the order of the list above decided what an old note says. A name this build does not know
/// reads as <see cref="NoteTextMark.None"/>, which draws nothing and is dropped the moment the line is
/// normalized - so a note saved by a newer build opens here rather than refusing to.
/// </summary>
public sealed class NoteTextMarkJsonConverter : JsonConverter<NoteTextMark>
{
    public override NoteTextMark Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.TokenType == JsonTokenType.String
            && Enum.TryParse<NoteTextMark>(reader.GetString(), ignoreCase: true, out var known)
                ? known
                : NoteTextMark.None;

    public override void Write(Utf8JsonWriter writer, NoteTextMark value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString());
}
