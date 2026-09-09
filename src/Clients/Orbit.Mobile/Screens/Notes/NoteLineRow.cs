using CommunityToolkit.Mvvm.ComponentModel;
using Orbit.Contracts.Notes;

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

    public static NoteLineRow From(NoteContentLineDto line)
        => new() { Text = line.Text, IsChecklistItem = line.IsChecklistItem, IsChecked = line.IsChecked };

    public NoteContentLineDto ToDto() => new(Text, IsChecklistItem, IsChecked);

    /// <summary>What the tick box shows: empty, ticked, or nothing at all for prose.</summary>
    public string CompletionMark => !IsChecklistItem ? string.Empty : IsChecked ? "☑" : "☐";

    public bool IsCompleted => IsChecklistItem && IsChecked;

    /// <summary>
    /// Whether the editor shows this line as something to write in rather than as something already
    /// done. A ticked line is drawn struck through, which a text box cannot do - MAUI puts
    /// TextDecorations on a Label and nowhere else - so the two are separate controls and this is which
    /// of them is showing. Untick it to write in it again, which is also what it means.
    /// </summary>
    public bool IsOpenForWriting => !IsCompleted;

    partial void OnIsChecklistItemChanged(bool value) => SayHowItIsDrawn();

    partial void OnIsCheckedChanged(bool value) => SayHowItIsDrawn();

    private void SayHowItIsDrawn()
    {
        OnPropertyChanged(nameof(CompletionMark));
        OnPropertyChanged(nameof(IsCompleted));
        OnPropertyChanged(nameof(IsOpenForWriting));
    }
}
