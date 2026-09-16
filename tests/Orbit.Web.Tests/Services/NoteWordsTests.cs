using Orbit.Core.Notes;
using Xunit;

namespace Orbit.Web.Tests.Services;

/// <summary>
/// A note as plain words - what "copy the text" puts on the clipboard, on both clients since the phone
/// gained the action. The format matters beyond looking right: a paste reads it back
/// (NoteSurfaceEdits.ReadPastedLine), so what is copied out of one note has to arrive in another as the
/// same note.
/// </summary>
public sealed class NoteWordsTests
{
    [Fact]
    public void The_name_is_the_first_line_and_the_boxes_keep_their_marks()
    {
        var words = NoteWords.Of(
            "Shopping",
            [
                NoteContentLine.PlainText("For Sunday"),
                new NoteContentLine("Milk", IsChecklistItem: true, IsChecked: true),
                new NoteContentLine("Bread", IsChecklistItem: true, IsChecked: false)
            ]);

        Assert.Equal("Shopping\nFor Sunday\n[x] Milk\n- Bread", words);
    }

    /// <summary>
    /// The round trip the format exists for: every line comes back as the line it was, boxes included.
    /// </summary>
    [Fact]
    public void What_it_writes_is_read_back_as_the_same_lines()
    {
        IReadOnlyList<NoteContentLine> lines =
        [
            NoteContentLine.PlainText("For Sunday"),
            new NoteContentLine("Milk", IsChecklistItem: true, IsChecked: true),
            new NoteContentLine("Bread", IsChecklistItem: true, IsChecked: false)
        ];

        var readBack = NoteWords.Of(string.Empty, lines).Split('\n').Skip(1).Select(NoteSurfaceEdits.ReadPastedLine).ToList();

        Assert.Equal(
            lines.Select(line => (line.Text, line.IsChecklistItem, line.IsChecked)),
            readBack.Select(line => (line.Text, line.IsChecklistItem, line.IsChecked)));
    }

    /// <summary>
    /// A table's words are in its cells and a picture has none, so neither is written as a blank line -
    /// which is what they were before this was shared, the browser having written line.Text for both.
    /// </summary>
    [Fact]
    public void A_table_and_a_picture_are_left_out_rather_than_written_as_nothing()
    {
        var words = NoteWords.Of(
            "Shopping",
            [
                NoteContentLine.OfTable(NoteTables.Empty()),
                NoteContentLine.PlainText("For Sunday"),
                NoteContentLine.OfPicture(new NotePictureLine(Guid.NewGuid(), "image/jpeg"))
            ]);

        Assert.Equal("Shopping\nFor Sunday", words);
    }
}
