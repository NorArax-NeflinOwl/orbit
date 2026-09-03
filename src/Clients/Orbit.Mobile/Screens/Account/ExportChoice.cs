using CommunityToolkit.Mvvm.ComponentModel;
using Orbit.Core.Transfer;

namespace Orbit.Mobile.Screens.Account;

/// <summary>
/// Which parts of the account the next export will carry. Everything to begin with, because that is
/// what an export meant before there was anything to choose, and it is what somebody who does not read
/// the row expects to get.
///
/// The same four choices Orbit.Web offers, written again rather than shared: the browser's live inside
/// its Options page, which is a client this project cannot reference. What is shared is the archive
/// itself - see <see cref="OrbitArchive"/> - which is the part that has to agree.
/// </summary>
public sealed partial class ExportChoice : ObservableObject
{
    [ObservableProperty]
    private bool _includesNotes = true;

    [ObservableProperty]
    private bool _includesTaskLists = true;

    [ObservableProperty]
    private bool _includesCalendarEvents = true;

    [ObservableProperty]
    private bool _includesWarehouses = true;

    /// <summary>
    /// Nothing chosen is not an export of nothing - it is a button with no reason to be pressed, so it
    /// is not offered.
    /// </summary>
    public bool IsEmpty
        => !IncludesNotes && !IncludesTaskLists && !IncludesCalendarEvents && !IncludesWarehouses;

    /// <summary>
    /// The archive with the parts nobody asked for emptied. Emptied rather than left out: the file's
    /// shape is what an importer reads, and one missing a list is a file an older Orbit would refuse.
    /// </summary>
    public OrbitArchive Narrow(OrbitArchive archive)
        => archive with
        {
            Notes = IncludesNotes ? archive.Notes : [],
            TaskLists = IncludesTaskLists ? archive.TaskLists : [],
            CalendarEvents = IncludesCalendarEvents ? archive.CalendarEvents : [],
            Warehouses = IncludesWarehouses ? archive.Warehouses : []
        };

    partial void OnIncludesNotesChanged(bool value) => OnPropertyChanged(nameof(IsEmpty));

    partial void OnIncludesTaskListsChanged(bool value) => OnPropertyChanged(nameof(IsEmpty));

    partial void OnIncludesCalendarEventsChanged(bool value) => OnPropertyChanged(nameof(IsEmpty));

    partial void OnIncludesWarehousesChanged(bool value) => OnPropertyChanged(nameof(IsEmpty));
}
