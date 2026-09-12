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

    public static NoteLineRow From(NoteContentLineDto line)
        => new()
        {
            Text = line.Text,
            IsChecklistItem = line.IsChecklistItem,
            IsChecked = line.IsChecked,
            IsFailed = line.IsFailed
        };

    public NoteContentLineDto ToDto() => new(Text, IsChecklistItem, IsChecked, IsFailed);

    /// <summary>The same line as the surface Orbit.Core decides edits on - see Orbit.Core.Notes.SurfaceState.</summary>
    public static NoteLineRow From(NoteContentLine line)
        => new()
        {
            Text = line.Text,
            IsChecklistItem = line.IsChecklistItem,
            IsChecked = line.IsChecked,
            IsFailed = line.IsFailed
        };

    /// <inheritdoc cref="From(NoteContentLine)"/>
    public NoteContentLine ToLine() => new(Text, IsChecklistItem, IsChecked, IsFailed);

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
    }

    /// <summary>
    /// What the line said before its last change. A field on the phone reports only what it says now, so
    /// this is how the note screen tells what was typed, deleted or pasted - see NoteTextChange.
    /// </summary>
    public string TextBefore { get; private set; } = string.Empty;

    partial void OnTextChanged(string? oldValue, string newValue) => TextBefore = oldValue ?? string.Empty;

    /// <summary>What the box says, as the three answers there are - see TickState.</summary>
    public TickState Tick => Ticks.Read(IsChecked, IsFailed);

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
    /// done - which of the two controls is showing. A ticked line opens while it is being written in.
    /// </summary>
    public bool IsOpenForWriting => !IsCompleted || IsBeingWrittenIn;

    /// <summary>Struck through: done, and not currently being written in.</summary>
    public bool IsStruckThrough => IsCompleted && !IsBeingWrittenIn;

    partial void OnIsChecklistItemChanged(bool value) => SayHowItIsDrawn();

    partial void OnIsCheckedChanged(bool value) => SayHowItIsDrawn();

    partial void OnIsFailedChanged(bool value) => SayHowItIsDrawn();

    partial void OnIsBeingWrittenInChanged(bool value) => SayHowItIsDrawn();

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
    }
}
