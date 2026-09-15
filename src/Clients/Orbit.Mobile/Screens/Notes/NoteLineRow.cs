using CommunityToolkit.Mvvm.ComponentModel;
using Orbit.Contracts.Notes;
using Orbit.Core.Abstractions;
using Orbit.Core.Notes;

namespace Orbit.Mobile.Screens.Notes;

/// <summary>
/// One line of a note as the editor shows it. A line is either ordinary prose or a checklist item that
/// can be ticked - the same two shapes Orbit.Web's ChecklistTextEditor offers, because they are the same
/// note either way and a phone that offered only one of them would quietly flatten the other.
///
/// A class rather than a record, and its text writable, for two reasons. The screen edits a line in
/// place, as the web's editor does - it was a read-only label here, so a line could be written once and
/// never corrected. And the commands find the line they were given by identity: with value equality,
/// two lines that happened to say the same thing were the same line, so ticking one ticked both.
/// </summary>
public sealed partial class NoteLineRow : ObservableObject
{
    [ObservableProperty]
    private string _text = string.Empty;

    [ObservableProperty]
    private bool _isChecklistItem;

    [ObservableProperty]
    private bool _isChecked;

    /// <summary>Crossed out rather than ticked off - see Orbit.Core.Notes.NoteContentLine.IsFailed.</summary>
    [ObservableProperty]
    private bool _isFailed;

    /// <summary>
    /// What this line is - a heading, a line of a list, ordinary writing. See
    /// Orbit.Core.Notes.NoteLineStyle, which is where the rules about it live; here it is carried so
    /// that a note written on one client and edited on the other keeps its shape, and drawn by the
    /// three properties below.
    /// </summary>
    [ObservableProperty]
    private NoteLineStyle _style = NoteLineStyle.Body;

    /// <summary>
    /// What a numbered line shows, or 0 for a line that is not numbered. Worked out from the lines above
    /// it - see NoteLineStyles.NumberOf, which the view model calls whenever the lines change, because a
    /// line's number is a fact about what is above it and a line knows nothing about that on its own.
    /// </summary>
    [ObservableProperty]
    private int _listNumber;

    /// <summary>
    /// The marks on stretches of words inside this line - see Orbit.Core.Notes.NoteTextRun. Drawn by a
    /// MarkedLabel while the line is not being written in (see <see cref="ShowsMarkedWords"/>): a MAUI
    /// Entry renders one face for the whole field, so the field shows the words plainly while the caret
    /// is in them, and the marks are moved along with what is typed (NoteDetailViewModel.WhenALineChanges)
    /// so they are still over the right words when the label comes back.
    /// </summary>
    [ObservableProperty]
    private IReadOnlyList<NoteTextRun> _marks = NoteTextMarks.None;

    /// <summary>Whether any stretch of the words carries a mark - which is what decides the label from the field.</summary>
    public bool HasMarks => Marks.Count > 0;

    /// <summary>Whether this line is something other than words - a table or a picture. See NoteContentLine.IsAnElement.</summary>
    public bool IsAnElement => IsATable || IsAPicture;

    /// <summary>
    /// The table this line is, when it is one - see Orbit.Core.Notes.NoteTable. Drawn on the screen as a
    /// grid of fields, one a cell (see <see cref="TableRows"/>), and written in through them: what a
    /// cell's field says goes into the table here (<see cref="WhenACellChanges"/>), which is what makes
    /// the table the one thing every edit, undo and save reads.
    /// </summary>
    [ObservableProperty]
    private NoteTable? _table;

    /// <summary>
    /// The cells' fields, a list a row, built from <see cref="Table"/> and rebuilt only when it changes
    /// shape: a change of words alone is written into the fields that are there, so the field being
    /// written in keeps its caret and its keyboard.
    /// </summary>
    private IReadOnlyList<IReadOnlyList<NoteTableCellField>> _tableRows = [];

    /// <summary>Above zero while the fields are being told what the table says, so nothing is written back.</summary>
    private int _settingCells;

    /// <summary>True while a cell's own words are being put into the table, which is no reason to redraw the cells.</summary>
    private bool _takingACellsWords;

    /// <summary>Raised when something was written in one of the table's cells - for the view model, which records the step.</summary>
    public event EventHandler<NoteCellChange>? CellWrittenIn;

    /// <summary>Whether this line is a table rather than writing - which hides the field and shows the grid.</summary>
    public bool IsATable => Table is not null;

    /// <summary>
    /// The picture this line is, when it is one - see Orbit.Core.Notes.NotePictureLine. Carried through
    /// every edit unchanged; what the line holds is the picture's id and kind, and the bytes come
    /// separately (<see cref="PictureBytes"/>), fetched by the view model through NotePictureCache.
    /// </summary>
    [ObservableProperty]
    private NotePictureLine? _picture;

    public bool IsAPicture => Picture is not null;

    /// <summary>
    /// The picture ready to draw, once the view model has fetched or opened it, and null until then or
    /// when it cannot be had - in which case <see cref="PictureNote"/> says why. Bytes rather than a
    /// path, because a private note's picture is kept sealed on the handset and only ever opened into
    /// memory; the page turns them into an image (PictureBytesConverter in Orbit.Maui).
    /// </summary>
    [ObservableProperty]
    private byte[]? _pictureBytes;

    /// <summary>What is said in the picture's place while there is nothing to draw - fetching, or why it cannot be shown.</summary>
    [ObservableProperty]
    private string _pictureNote = string.Empty;

    public bool ShowsPicture => IsAPicture && PictureBytes is not null;

    public bool ShowsPictureNote => IsAPicture && PictureBytes is null;

    /// <summary>The table's rows as the screen draws and writes in them: each a list of its cells' fields.</summary>
    public IReadOnlyList<IReadOnlyList<NoteTableCellField>> TableRows => _tableRows;

    public static NoteLineRow From(NoteContentLineDto line)
        => new()
        {
            Text = line.Text,
            IsChecklistItem = line.IsChecklistItem,
            IsChecked = line.IsChecked,
            IsFailed = line.IsFailed,
            Style = NoteLineStyles.Read(line.Style),
            Marks = ReadMarks(line.AllMarks, line.Text),
            Table = line.Table is null
                ? null
                : NoteTables.Squared(new NoteTable([.. line.Table.Rows.Select(row => new NoteTableRow(
                    [.. row.Cells.Select(cell => new NoteTableCell(cell.Text, ReadMarks(cell.AllMarks, cell.Text)))]))])),
            Picture = line.Picture is null
                ? null
                : new NotePictureLine(line.Picture.PictureId, line.Picture.ContentType, line.Picture.WidthPixels, line.Picture.HeightPixels)
        };

    private static IReadOnlyList<NoteTextRun> ReadMarks(IReadOnlyList<NoteTextRunDto> marks, string text)
        => NoteTextMarks.Normalized(
            marks.Select(run => new NoteTextRun(run.Start, run.Length, NoteTextMarks.Read(run.Mark))), text.Length);

    private static IReadOnlyList<NoteTextRunDto>? SentMarks(IReadOnlyList<NoteTextRun> marks)
        => marks.Count == 0
            ? null
            : marks.Select(run => new NoteTextRunDto(run.Start, run.Length, run.Mark.ToString())).ToList();

    public NoteContentLineDto ToDto()
        => new(
            Text, IsChecklistItem, IsChecked, IsFailed, Style.ToString(), SentMarks(Marks),
            Table is null
                ? null
                : new NoteTableDto([.. Table.Rows.Select(row => new NoteTableRowDto(
                    [.. row.Cells.Select(cell => new NoteTableCellDto(cell.Text, SentMarks(cell.AllMarks)))]))]),
            Picture is null
                ? null
                : new NotePictureLineDto(Picture.PictureId, Picture.ContentType, Picture.WidthPixels, Picture.HeightPixels));

    /// <summary>The same line as the surface Orbit.Core decides edits on - see Orbit.Core.Notes.SurfaceState.</summary>
    public static NoteLineRow From(NoteContentLine line)
        => new()
        {
            Text = line.Text,
            IsChecklistItem = line.IsChecklistItem,
            IsChecked = line.IsChecked,
            IsFailed = line.IsFailed,
            Style = line.Style,
            Marks = line.AllMarks,
            Table = line.Table,
            Picture = line.Picture
        };

    /// <inheritdoc cref="From(NoteContentLine)"/>
    public NoteContentLine ToLine() => new(Text, IsChecklistItem, IsChecked, IsFailed, Style, Marks, Table, Picture);

    /// <summary>
    /// Becomes <paramref name="line"/> in place - what an undo does to a line that is still there, so the
    /// field drawing it stays the same field and nothing on the screen is rebuilt.
    /// </summary>
    public void Take(NoteContentLine line)
    {
        Text = line.Text;
        IsChecklistItem = line.IsChecklistItem;
        IsChecked = line.IsChecked;
        IsFailed = line.IsFailed;
        Style = line.Style;
        Marks = line.AllMarks;
        Table = line.Table;
        Picture = line.Picture;
    }

    /// <summary>
    /// What the line said before its last change. A field on the phone reports only what it says now, so
    /// this is how the note screen tells what was typed, deleted or pasted - see NoteTextChange.
    /// </summary>
    public string TextBefore { get; private set; } = string.Empty;

    partial void OnTextChanged(string? oldValue, string newValue) => TextBefore = oldValue ?? string.Empty;

    /// <summary>What the box says, as the three answers there are - see TickState.</summary>
    public TickState Tick => Ticks.Read(IsChecked, IsFailed);

    /// <summary>How large the line is drawn - see <see cref="NoteLineLook.SizeOf"/>.</summary>
    public double DrawnFontSize => NoteLineLook.SizeOf(Style);

    /// <inheritdoc cref="NoteLineLook.IsBold"/>
    public bool IsDrawnBold => NoteLineLook.IsBold(Style);

    /// <inheritdoc cref="NoteLineLook.MarkOf"/>
    public string ListMark => NoteLineLook.MarkOf(Style, ListNumber);

    /// <summary>
    /// Whether that mark is shown. Not on a line that also has a box: the box is already the mark at the
    /// head of the line, and two of them read as two lists - the same rule the browser's stylesheet has.
    /// </summary>
    public bool ShowsListMark => ListMark.Length > 0 && !IsChecklistItem;

    /// <summary>What the tick box shows: empty, ticked, or nothing at all for prose.</summary>
    public string CompletionMark => !IsChecklistItem ? string.Empty : IsChecked ? "☑" : "☐";

    /// <summary>
    /// Finished with, either way - what the line is drawn struck through for. The circle beside it says
    /// which of the two it was.
    /// </summary>
    public bool IsCompleted => IsChecklistItem && (IsChecked || IsFailed);

    /// <summary>
    /// Set while the reader has the caret in this line, and only ever true for a ticked one.
    ///
    /// A ticked line is drawn struck through, which a text box cannot do - MAUI puts TextDecorations on
    /// a Label and nowhere else - so the two are separate controls. Drawn as a Label and nothing else, a
    /// ticked line cannot be reached by the keyboard at all: there is no field to put a caret in, so
    /// backspace on it did nothing and the only way to get rid of one was to untick it first. Pressing
    /// it opens the field in the Label's place, which is what the design has - there every line is a
    /// field, ticked or not, and only its decoration changes.
    /// </summary>
    [ObservableProperty]
    private bool _isBeingWrittenIn;

    /// <summary>
    /// Whether the editor shows this line as something to write in rather than as words drawn - which of
    /// the controls is showing. A ticked line and a line with marks on it are drawn as words until they
    /// are being written in, since a field can strike nothing through and bold nothing; a table or a
    /// picture never opens, its words being in its cells or none at all.
    /// </summary>
    public bool IsOpenForWriting => !IsAnElement && (IsBeingWrittenIn || (!IsCompleted && !HasMarks));

    /// <summary>Struck through: done, and not currently being written in.</summary>
    public bool IsStruckThrough => IsCompleted && !IsBeingWrittenIn;

    /// <summary>
    /// Drawn as words with their marks - see MarkedLabel: not done (a done line is struck through whole,
    /// which says more than a bold word inside it), and not being written in. Pressing it opens the
    /// field in its place, the way a struck-through line opens.
    /// </summary>
    public bool ShowsMarkedWords => HasMarks && !IsCompleted && !IsBeingWrittenIn && !IsAnElement;

    partial void OnIsChecklistItemChanged(bool value) => SayHowItIsDrawn();

    partial void OnIsCheckedChanged(bool value) => SayHowItIsDrawn();

    partial void OnIsFailedChanged(bool value) => SayHowItIsDrawn();

    partial void OnIsBeingWrittenInChanged(bool value) => SayHowItIsDrawn();

    partial void OnStyleChanged(NoteLineStyle value) => SayHowItIsDrawn();

    partial void OnListNumberChanged(int value) => SayHowItIsDrawn();

    partial void OnTableChanged(NoteTable? value)
    {
        if (!_takingACellsWords)
        {
            RedrawCells(value);
        }

        OnPropertyChanged(nameof(IsATable));
        OnPropertyChanged(nameof(IsAnElement));
        SayHowItIsDrawn();
    }

    /// <summary>
    /// Makes the cells' fields say what <paramref name="table"/> says: in place when it has the shape
    /// they were built for - an undo, a save read back - and from scratch when it does not - a row
    /// added, the table gone. TableRows is announced only in the second case, since announcing it is
    /// what makes the screen build the grid again.
    /// </summary>
    private void RedrawCells(NoteTable? table)
    {
        if (table is not null && HasTheShapeOf(table))
        {
            _settingCells++;
            try
            {
                for (var row = 0; row < table.Rows.Count; row++)
                {
                    for (var column = 0; column < table.Columns; column++)
                    {
                        var cell = table.Rows[row].Cells[column];
                        _tableRows[row][column].Marks = cell.AllMarks;
                        _tableRows[row][column].Text = cell.Text;
                    }
                }
            }
            finally
            {
                _settingCells--;
            }

            return;
        }

        foreach (var field in _tableRows.SelectMany(row => row))
        {
            field.PropertyChanged -= WhenACellChanges;
        }

        _tableRows = table is null ? [] : FieldsFor(table);
        foreach (var field in _tableRows.SelectMany(row => row))
        {
            field.PropertyChanged += WhenACellChanges;
        }

        OnPropertyChanged(nameof(TableRows));
    }

    private IReadOnlyList<IReadOnlyList<NoteTableCellField>> FieldsFor(NoteTable table)
        => [.. table.Rows.Select((row, rowIndex) => (IReadOnlyList<NoteTableCellField>)[.. row.Cells.Select(
            (cell, columnIndex) => new NoteTableCellField(this, rowIndex, columnIndex, cell))])];

    private bool HasTheShapeOf(NoteTable table)
        => _tableRows.Count == table.Rows.Count && _tableRows.All(row => row.Count == table.Columns);

    /// <summary>
    /// What was written in a cell goes into the table, with the cell's marks moved along with it (the
    /// arithmetic a line's marks follow, see NoteDetailViewModel.WhenALineChanges), and the view model is
    /// told. The cells are not redrawn for it: the field already says what the table now says.
    /// </summary>
    private void WhenACellChanges(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (_settingCells > 0
            || args.PropertyName != nameof(NoteTableCellField.Text)
            || sender is not NoteTableCellField field
            || Table is not { } table)
        {
            return;
        }

        var change = NoteTextChange.Between(field.TextBefore, field.Text);
        field.Marks = NoteTextMarks.Kept(field.Marks, change.Start, change.Removed, change.Inserted.Length, field.Text.Length);

        _takingACellsWords = true;
        try
        {
            Table = NoteTables.WithCell(table, field.Row, field.Column, field.ToCell());
        }
        finally
        {
            _takingACellsWords = false;
        }

        CellWrittenIn?.Invoke(this, new NoteCellChange(field, change));
    }

    partial void OnPictureChanged(NotePictureLine? value)
    {
        OnPropertyChanged(nameof(IsAPicture));
        OnPropertyChanged(nameof(IsAnElement));
        OnPropertyChanged(nameof(ShowsPicture));
        OnPropertyChanged(nameof(ShowsPictureNote));
        SayHowItIsDrawn();
    }

    partial void OnPictureBytesChanged(byte[]? value)
    {
        OnPropertyChanged(nameof(ShowsPicture));
        OnPropertyChanged(nameof(ShowsPictureNote));
    }

    partial void OnMarksChanged(IReadOnlyList<NoteTextRun> value)
    {
        OnPropertyChanged(nameof(HasMarks));
        SayHowItIsDrawn();
    }

    /// <summary>
    /// Chosen to change together with the other chosen boxes - see NoteDetailViewModel.IsPickingLines.
    /// A fact about the screen, not the note: it is never saved, and an undo does not bring it back.
    /// </summary>
    [ObservableProperty]
    private bool _isPicked;

    /// <summary>
    /// Whether the note is choosing boxes to change together, which puts a mark to choose one with beside
    /// every box. Set on every line by the view model, so a line that gains a box while it is choosing
    /// shows the mark at once.
    /// </summary>
    [ObservableProperty]
    private bool _offersPicking;

    /// <summary>The mark that chooses this line, shown only on a line with a box while boxes are being chosen.</summary>
    public bool ShowsPickMark => OffersPicking && IsChecklistItem;

    partial void OnOffersPickingChanged(bool value) => OnPropertyChanged(nameof(ShowsPickMark));

    private void SayHowItIsDrawn()
    {
        OnPropertyChanged(nameof(CompletionMark));
        OnPropertyChanged(nameof(IsCompleted));
        OnPropertyChanged(nameof(IsOpenForWriting));
        OnPropertyChanged(nameof(IsStruckThrough));
        OnPropertyChanged(nameof(Tick));
        OnPropertyChanged(nameof(ShowsPickMark));
        OnPropertyChanged(nameof(DrawnFontSize));
        OnPropertyChanged(nameof(IsDrawnBold));
        OnPropertyChanged(nameof(ListMark));
        OnPropertyChanged(nameof(ShowsListMark));
        OnPropertyChanged(nameof(ShowsMarkedWords));
    }
}
