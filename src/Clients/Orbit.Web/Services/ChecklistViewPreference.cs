using Microsoft.JSInterop;

namespace Orbit.Web.Services;

/// <summary>How a group checklist opens: as the tree of linked lists it is, or as one flat run of items.</summary>
public enum ChecklistView
{
    Tree,
    Flat
}

/// <summary>What order a checklist's items are read in.</summary>
public enum ChecklistOrder
{
    /// <summary>The order the list was arranged in - which is what the editor's dragging changes.</summary>
    AsArranged,

    /// <summary>By what each item says, for reading a long list off in one pass.</summary>
    Alphabetical
}

/// <summary>How one person reads one checklist. The two travel together: both are saved by one button.</summary>
public sealed record ChecklistReading(ChecklistView View, ChecklistOrder Order)
{
    public static readonly ChecklistReading Default = new(ChecklistView.Tree, ChecklistOrder.AsArranged);
}

/// <summary>
/// Remembers, per task list, how its checklist should open (see wwwroot/js/checklistView.js). Not stored
/// server-side: like the dashboard's pinned cards, this is how one person reads one page on one device,
/// and it says nothing about the lists themselves.
/// </summary>
public sealed class ChecklistViewPreference(IJSRuntime jsRuntime)
{
    /// <summary>What was saved for this list, or null when nothing ever was.</summary>
    public async Task<ChecklistReading?> GetAsync(Guid taskListId)
    {
        await using var module = await ImportModuleAsync();
        var saved = await module.InvokeAsync<SavedReading?>("getSavedReading", taskListId);
        if (saved is null)
        {
            return null;
        }

        return new ChecklistReading(
            saved.View == "flat" ? ChecklistView.Flat : ChecklistView.Tree,
            saved.Order == "alphabetical" ? ChecklistOrder.Alphabetical : ChecklistOrder.AsArranged);
    }

    public async Task SaveAsync(Guid taskListId, ChecklistReading reading)
    {
        await using var module = await ImportModuleAsync();
        await module.InvokeVoidAsync(
            "saveReading", taskListId,
            reading.View == ChecklistView.Flat ? "flat" : "tree",
            reading.Order == ChecklistOrder.Alphabetical ? "alphabetical" : "as-arranged");
    }

    private async Task<IJSObjectReference> ImportModuleAsync()
        => await jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/checklistView.js");

    /// <summary>The stored shape, which is strings rather than enums - see checklistView.js.</summary>
    public sealed record SavedReading(string View, string Order);
}
