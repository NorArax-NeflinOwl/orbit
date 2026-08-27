using Microsoft.JSInterop;

namespace Orbit.Web.Services;

/// <summary>How a group checklist opens: as the tree of linked lists it is, or as one flat run of items.</summary>
public enum ChecklistView
{
    Tree,
    Flat
}

/// <summary>
/// Remembers, per task list, which way its checklist should open (see wwwroot/js/checklistView.js).
/// Not stored server-side: like the dashboard's pinned cards, this is how one person reads one page on
/// one device, and it says nothing about the lists themselves.
/// </summary>
public sealed class ChecklistViewPreference(IJSRuntime jsRuntime)
{
    /// <summary>The saved view for this list, or null when it has never had one saved.</summary>
    public async Task<ChecklistView?> GetAsync(Guid taskListId)
    {
        await using var module = await ImportModuleAsync();
        var saved = await module.InvokeAsync<string?>("getSavedView", taskListId);
        return saved switch
        {
            "flat" => ChecklistView.Flat,
            "tree" => ChecklistView.Tree,
            _ => null
        };
    }

    public async Task SaveAsync(Guid taskListId, ChecklistView view)
    {
        await using var module = await ImportModuleAsync();
        await module.InvokeVoidAsync("saveView", taskListId, view == ChecklistView.Flat ? "flat" : "tree");
    }

    private async Task<IJSObjectReference> ImportModuleAsync()
        => await jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/checklistView.js");
}
