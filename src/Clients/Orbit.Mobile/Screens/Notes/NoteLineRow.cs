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
    /// The marks on stretches of words inside this line - see Orbit.Core.Notes.NoteTextRun. Carried
    /// rather than drawn for now: a MAUI Entry renders one face for the whole field, so the phone keeps
    /// what the browser wrote and hands it back unchanged, which is what stops an edit here from
    /// flattening a note written there.
    /// </summary>
    public IReadOnlyList<NoteTextRun> Marks { get; set; } = NoteTextMarks.None;

    /// <summary>
    /// The table this line is, when it is one - see Orbit.Core.Notes.NoteTable. Drawn on the screen as a
    /// grid of its cells' words (see <see cref="TableRows"/>); the cells cannot be written in on the phone
    /// yet, and the table is carried through every edit unchanged so nothing here flattens one.
    /// </summary>
    [ObservableProperty]
    private NoteTable? _table;

    /// <summary>Whether this line is a table rather than writing - which hides the field and shows the grid.</summary>
    public bool IsATable => Table is not null;

    /// <summary>
    /// The picture this line is, when it is one - see Orbit.Core.Notes.NotePictureLine. Carried through
    /// every edit unchanged and drawn as a placeholder: the phone fetches no picture bytes yet (they
    /// would have to be cached for offline reading), see info/future-plan.md.
    /// </summary>
    [ObservableProperty]
    private NotePictureLine? _picture;

    public bool IsAPicture => Picture is not null;

    /// <summary>The table's rows as the screen draws them: each a list of its cells' words.</summary>
    public IReadOnlyList<IReadOnlyList<string>> TableRows
        => Table is null ? [] : [.. Table.Rows.Select(row => (IReadOnlyList<string>)[.. row.Cells.Select(cell => cell.Text)])];

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
    /// Whether the editor shows this line as something to write in rather than as something already
    /// done - which of the two controls is showing. A ticked line opens while it is being written in; a
    /// table never does, since its words are in its cells and the field would be an empty line over it.
    /// </summary>
    public bool IsOpenForWriting => !IsATable && !IsAPicture && (!IsCompleted || IsBeingWrittenIn);

    /// <summary>Struck through: done, and not currently being written in.</summary>
    public bool IsStruckThrough => IsCompleted && !IsBeingWrittenIn;

    partial void OnIsChecklistItemChanged(bool value) => SayHowItIsDrawn();

    partial void OnIsCheckedChanged(bool value) => SayHowItIsDrawn();

    partial void OnIsFailedChanged(bool value) => SayHowItIsDrawn();

    partial void OnIsBeingWrittenInChanged(bool value) => SayHowItIsDrawn();

    partial void OnStyleChanged(NoteLineStyle value) => SayHowItIsDrawn();

    partial void OnListNumberChanged(int value) => SayHowItIsDrawn();

    partial void OnTableChanged(NoteTable? value)
    {
        OnPropertyChanged(nameof(IsATable));
        OnPropertyChanged(nameof(TableRows));
        SayHowItIsDrawn();
    }

    partial void OnPictureChanged(NotePictureLine? value)
    {
        OnPropertyChanged(nameof(IsAPicture));
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
    }
}
