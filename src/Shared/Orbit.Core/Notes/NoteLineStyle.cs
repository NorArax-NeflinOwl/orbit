using System.Text.Json;
using System.Text.Json.Serialization;

namespace Orbit.Core.Notes;

/// <summary>
/// What one line of a note is - a heading, a line of a list, ordinary writing. Apple Notes' own Format
/// menu, which is what the user asked this to follow: Title, Heading, Subheading, Body, Monospaced, and
/// the three kinds of list. A tick box is the fourth kind of list and is <b>not</b> here - it was a field
/// of its own (<see cref="NoteContentLine.IsChecklistItem"/>) long before this existed, everything from
/// the phone's rows to the restock list reads it, and folding it in would rewrite all of that to say the
/// same thing a second way.
///
/// A style belongs to a <b>line</b>, not to a stretch of words inside one. That is the whole of what this
/// can express, and it is deliberate: a note is a list of lines on both clients, and the phone draws each
/// one as its own field. Bold and italic inside a line are a different shape - see
/// info/future-plan.md, where what they would take is written down rather than guessed at.
/// </summary>
[JsonConverter(typeof(NoteLineStyleJsonConverter))]
public enum NoteLineStyle
{
    /// <summary>Ordinary writing, and what a line is unless it says otherwise.</summary>
    Body,

    /// <summary>The largest, for the one line a note is about. Apple Notes gives a new note one by default; Orbit does not - the note's name is already its first line.</summary>
    Title,

    Heading,

    Subheading,

    /// <summary>For something that has to keep its spacing - a command, a key, a column of numbers.</summary>
    Monospaced,

    /// <summary>A list marked with a dot. The mark is drawn rather than typed, so it is never part of the words.</summary>
    Bulleted,

    /// <summary>The same, marked with a dash.</summary>
    Dashed,

    /// <summary>The same, numbered - and the number is worked out from the lines above it rather than typed in. See <see cref="NoteLineStyles.NumberOf"/>.</summary>
    Numbered
}

/// <summary>The two questions every rule about styles asks, so neither is written out twice.</summary>
public static class NoteLineStyles
{
    /// <summary>
    /// Whether this is a heading of some size. What it is for: a heading is one line by definition, so
    /// Enter after one starts ordinary writing rather than a second heading - Apple Notes' own behaviour,
    /// and the one thing about headings somebody notices immediately if it is missing.
    /// </summary>
    public static bool IsAHeading(this NoteLineStyle style)
        => style is NoteLineStyle.Title or NoteLineStyle.Heading or NoteLineStyle.Subheading;

    /// <summary>
    /// Whether this is a list that carries on. Enter on one starts another line of the same list, and
    /// Enter on an <em>empty</em> one ends the list instead - which is exactly the rule a tick box has
    /// followed since 2026-09-12, applied to the other three kinds.
    /// </summary>
    public static bool IsAList(this NoteLineStyle style)
        => style is NoteLineStyle.Bulleted or NoteLineStyle.Dashed or NoteLineStyle.Numbered;

    /// <summary>
    /// A style read off the word it is stored and sent as. A word this build does not know reads as
    /// <see cref="NoteLineStyle.Body"/> rather than throwing: a note written on a newer build must still
    /// open, drawn plainly, rather than refusing to load - the rule every stored-by-name enum in Orbit
    /// follows. Here rather than beside each mapper, because the server, the browser and the phone all
    /// have to answer it the same way.
    /// </summary>
    public static NoteLineStyle Read(string? style)
        => Enum.TryParse<NoteLineStyle>(style, ignoreCase: true, out var known) ? known : NoteLineStyle.Body;

    /// <summary>
    /// What number a numbered line shows: its place in the unbroken run of numbered lines above it,
    /// counting from one. Worked out rather than stored, so inserting a line in the middle of a list
    /// renumbers the rest without anything having to be rewritten - and so a number can never disagree
    /// with where the line actually is.
    ///
    /// A run is broken by any line that is not numbered, which is what makes two lists separated by a
    /// paragraph two lists. Answers 0 for a line that is not numbered at all, which nothing draws.
    /// </summary>
    public static int NumberOf(IReadOnlyList<NoteContentLine> lines, int index)
        => NumberOf(lines.Count, index, at => lines[at].Style);

    /// <summary>
    /// The same rule where the lines are not <see cref="NoteContentLine"/>s - the page that only reads a
    /// note holds the lines it arrived as. What a line's style is, is the caller's to answer; the run is
    /// this one's, so there is one place that decides where a numbered list starts and stops.
    /// </summary>
    public static int NumberOf(int count, int index, Func<int, NoteLineStyle> styleAt)
    {
        if (index < 0 || index >= count || styleAt(index) != NoteLineStyle.Numbered)
        {
            return 0;
        }

        var number = 1;
        for (var above = index - 1; above >= 0 && styleAt(above) == NoteLineStyle.Numbered; above--)
        {
            number++;
        }

        return number;
    }
}

/// <summary>
/// Writes a style by its name and reads one back the same way. Stored rather than only sent: a note's
/// content is kept as JSON (see NoteEntity.ContentJson and the phone's own store), so what this writes is
/// what a note is saved as, and a number there would mean the order of the list above decided what an
/// old note says. A name this build does not know reads as <see cref="NoteLineStyle.Body"/> rather than
/// throwing, so a note saved by a newer build still opens on an older one - see
/// <see cref="NoteLineStyles.Read"/>, which is that rule.
/// </summary>
public sealed class NoteLineStyleJsonConverter : JsonConverter<NoteLineStyle>
{
    public override NoteLineStyle Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.TokenType switch
        {
            JsonTokenType.String => NoteLineStyles.Read(reader.GetString()),
            // A number is how this would have been written without this converter. Nothing has ever
            // stored one, and reading it costs a line - which is cheaper than finding out otherwise.
            JsonTokenType.Number when Enum.IsDefined(typeof(NoteLineStyle), reader.GetInt32())
                => (NoteLineStyle)reader.GetInt32(),
            _ => NoteLineStyle.Body
        };

    public override void Write(Utf8JsonWriter writer, NoteLineStyle value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString());
}
