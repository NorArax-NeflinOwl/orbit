using Orbit.Core.Notes;

namespace Orbit.Maui.Controls;

/// <summary>
/// A label that draws the marks on stretches of a line's words - bold, italic, underlined, struck through
/// - the browser's MarkedText, on a phone. The stretches come from <see cref="NoteTextMarks.Pieces"/>, so
/// what is bold here is what is bold there; this only turns each piece into a Span.
///
/// A Label rather than a control wrapping one, for the reason <see cref="LinkedLabel"/> gives: it drops
/// into the places a Label already sits with their styles intact, and <see cref="Label.FormattedText"/>
/// is the only way MAUI lets one line carry several faces. The words go in through <see cref="Words"/>
/// rather than Text, for the same reason too. Addresses are not made pressable here - a line with marks
/// is drawn by this and a line without by LinkedLabel, and doing both in one control is a change worth
/// making once somebody has a bold link.
/// </summary>
public sealed class MarkedLabel : Label
{
    public static readonly BindableProperty WordsProperty =
        BindableProperty.Create(nameof(Words), typeof(string), typeof(MarkedLabel), string.Empty,
            propertyChanged: (label, _, _) => ((MarkedLabel)label).Redraw());

    public static readonly BindableProperty MarksProperty =
        BindableProperty.Create(nameof(Marks), typeof(IReadOnlyList<NoteTextRun>), typeof(MarkedLabel), NoteTextMarks.None,
            propertyChanged: (label, _, _) => ((MarkedLabel)label).Redraw());

    /// <summary>What was written. Empty draws an empty line, as a Label showing an empty string does.</summary>
    public string Words
    {
        get => (string)GetValue(WordsProperty);
        set => SetValue(WordsProperty, value);
    }

    /// <summary>The marks on stretches of it - see NoteContentLine.Marks. None draws the words plainly.</summary>
    public IReadOnlyList<NoteTextRun> Marks
    {
        get => (IReadOnlyList<NoteTextRun>)GetValue(MarksProperty);
        set => SetValue(MarksProperty, value);
    }

    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName == FontSizeProperty.PropertyName || propertyName == FontAttributesProperty.PropertyName)
        {
            Redraw();
        }
    }

    private void Redraw()
    {
        var formatted = new FormattedString();
        foreach (var piece in NoteTextMarks.Pieces(Words ?? string.Empty, Marks ?? NoteTextMarks.None))
        {
            formatted.Spans.Add(SpanOf(piece));
        }

        FormattedText = formatted;
    }

    /// <summary>
    /// One stretch as a Span, starting from the face the whole label has - its size, and bold where a
    /// heading made it so - because a Span with a face of its own no longer falls back to the label's,
    /// and an italic word inside a heading should still be heading-sized and bold. Bold and italic are
    /// font attributes and can be both at once; underline and strikethrough are decorations and can be
    /// both at once too - the flags combine, which is what makes four marks four rather than sixteen cases.
    /// </summary>
    private Span SpanOf(NoteTextPiece piece)
    {
        var span = new Span { Text = piece.Text, FontAttributes = FontAttributes };
        if (FontSize > 0)
        {
            span.FontSize = FontSize;
        }

        foreach (var mark in piece.Marks)
        {
            switch (mark)
            {
                case NoteTextMark.Bold:
                    span.FontAttributes |= FontAttributes.Bold;
                    break;
                case NoteTextMark.Italic:
                    span.FontAttributes |= FontAttributes.Italic;
                    break;
                case NoteTextMark.Underlined:
                    span.TextDecorations |= TextDecorations.Underline;
                    break;
                case NoteTextMark.StruckThrough:
                    span.TextDecorations |= TextDecorations.Strikethrough;
                    break;
            }
        }

        return span;
    }
}
