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
    Alphabetical,

    /// <summary>What is left to do first, then what is done, each alphabetically - the working order.</summary>
    UndoneFirst
}

/// <summary>
/// What order the panel pricing a list against a warehouse lists what it needs in. Its own set rather
/// than <see cref="ChecklistOrder"/>: the rows there are products and shortfalls, not things to tick.
/// </summary>
public enum StockCheckOrder
{
    /// <summary>The order the work asks for them, which is the order the lists are written in.</summary>
    AsCounted,
    Alphabetical,
    ReverseAlphabetical,

    /// <summary>What the shelf does not cover first - the only rows anybody has to do anything about.</summary>
    ShortFirst
}

/// <summary>
/// How one person reads one checklist: which shape, in what order, whether the panel that prices it
/// against a warehouse is in the way, and what order that panel lists things in. They travel together
/// because one button saves all of them.
/// </summary>
public sealed record ChecklistReading(
    ChecklistView View, ChecklistOrder Order, bool IsStockCheckHidden = false,
    StockCheckOrder StockOrder = StockCheckOrder.AsCounted)
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
            saved.Order switch
            {
                "alphabetical" => ChecklistOrder.Alphabetical,
                "undone-first" => ChecklistOrder.UndoneFirst,
                _ => ChecklistOrder.AsArranged
            },
            saved.IsStockCheckHidden,
            saved.StockOrder switch
            {
                "alphabetical" => StockCheckOrder.Alphabetical,
                "reverse-alphabetical" => StockCheckOrder.ReverseAlphabetical,
                "short-first" => StockCheckOrder.ShortFirst,
                _ => StockCheckOrder.AsCounted
            });
    }

    public async Task SaveAsync(Guid taskListId, ChecklistReading reading)
    {
        await using var module = await ImportModuleAsync();
        await module.InvokeVoidAsync(
            "saveReading", taskListId,
            reading.View == ChecklistView.Flat ? "flat" : "tree",
            reading.Order switch
            {
                ChecklistOrder.Alphabetical => "alphabetical",
                ChecklistOrder.UndoneFirst => "undone-first",
                _ => "as-arranged"
            },
            reading.IsStockCheckHidden,
            reading.StockOrder switch
            {
                StockCheckOrder.Alphabetical => "alphabetical",
                StockCheckOrder.ReverseAlphabetical => "reverse-alphabetical",
                StockCheckOrder.ShortFirst => "short-first",
                _ => "as-counted"
            });
    }

    private async Task<IJSObjectReference> ImportModuleAsync()
        => await jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/checklistView.js");

    /// <summary>The stored shape, which is strings rather than enums - see checklistView.js.</summary>
    public sealed record SavedReading(
        string View, string Order, bool IsStockCheckHidden = false, string StockOrder = "as-counted");
}
