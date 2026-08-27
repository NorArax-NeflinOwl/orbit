using Orbit.Contracts.Notes;

namespace Orbit.Mobile.Screens.Notes;

/// <summary>
/// One line of a note as the editor shows it. A line is either ordinary prose or a checklist item that
/// can be ticked - the same two shapes Orbit.Web's ChecklistTextEditor offers, because they are the same
/// note either way and a phone that offered only one of them would quietly flatten the other.
/// </summary>
public sealed record NoteLineRow(string Text, bool IsChecklistItem, bool IsChecked)
{
    public static NoteLineRow From(NoteContentLineDto line)
        => new(line.Text, line.IsChecklistItem, line.IsChecked);

    public NoteContentLineDto ToDto() => new(Text, IsChecklistItem, IsChecked);

    /// <summary>What the tick box shows: empty, ticked, or nothing at all for prose.</summary>
    public string CompletionMark => !IsChecklistItem ? string.Empty : IsChecked ? "☑" : "☐";

    public bool IsCompleted => IsChecklistItem && IsChecked;
}
