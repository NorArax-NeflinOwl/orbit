using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Contracts.Tasks;
using Orbit.Mobile.Api;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;

namespace Orbit.Mobile.Screens.Tasks;

/// <summary>One warehouse a list's work can be measured against, or none at all.</summary>
public sealed record WarehouseChoice(Guid? ServerId, string Name);

/// <summary>One thing the work calls for, and whether the shelf covers it.</summary>
public sealed record StockRequirementRow(string Name, string Required, string Available, string Missing)
{
    public bool IsShort => Missing.Length > 0;

    public static StockRequirementRow From(StockRequirementDto requirement)
        => new(
            requirement.Name,
            requirement.Required.ToString("0.##"),
            requirement.Available.ToString("0.##"),
            requirement.Missing > 0 ? requirement.Missing.ToString("0.##") : string.Empty);
}

/// <summary>
/// "Can this be done?" - what a group list's work costs against a warehouse, as Orbit.Web's task list
/// page asks it. Its own object rather than more members on the task-list screen, because it is a
/// question about two things and the screen is about one of them.
///
/// Server-side arithmetic, so it is asked rather than worked out here, and there is nothing to show
/// without a connection. That is the honest shape: the answer depends on a shelf this phone does not
/// hold, and a stale one would be worse than none.
/// </summary>
public sealed partial class StockCheckPanel : ObservableObject
{
    private readonly TasksClient _tasks;
    private readonly InventoryClient _inventory;
    private readonly LocalWarehouseRepository _warehouses;
    private readonly Translations _translations;
    private readonly IChecklistReadingStore _reading;

    private Guid? _taskListServerId;
    private Guid _taskListLocalId;

    /// <summary>What the last answer said, before it was put in the reader's chosen order.</summary>
    private readonly List<StockRequirementRow> _asCounted = [];

    public StockCheckPanel(
        TasksClient tasks, InventoryClient inventory, LocalWarehouseRepository warehouses,
        Translations translations, ConnectionRequirement connection, IChecklistReadingStore reading)
    {
        _tasks = tasks;
        _inventory = inventory;
        _warehouses = warehouses;
        _translations = translations;
        _reading = reading;
        Connection = connection;
    }

    /// <summary>
    /// Only a group list gathers enough work to be worth counting, which is the rule Orbit.Web applies
    /// too - see StockRequirementCounter.
    /// </summary>
    [ObservableProperty]
    private bool _isOffered;

    /// <summary>What the shelf is measured against, plus "not measured at all" leading the list.</summary>
    public ObservableCollection<WarehouseChoice> Warehouses { get; } = [];

    [ObservableProperty]
    private WarehouseChoice? _linkedWarehouse;

    public ObservableCollection<StockRequirementRow> Requirements { get; } = [];

    /// <summary>
    /// Folded down to its heading. The panel asks a question about a shelf, which is not what somebody
    /// working through the list itself is looking at - so it is put away rather than scrolled past, and
    /// stays that way for this list on this device. Orbit.Web folds the same panel for the same reason.
    /// </summary>
    [ObservableProperty]
    private bool _isFolded;

    public bool IsNotFolded => !IsFolded;

    /// <summary>The same chevron every card on the phone folds by - see TaskListRow.FoldGlyph.</summary>
    public string FoldGlyph => IsFolded ? "▾" : "▴";

    public string FoldDescription => IsFolded ? _translations["Show"] : _translations["Hide"];

    /// <summary>What order the rows are read in. Remembered per list, as the folding is.</summary>
    [ObservableProperty]
    private StockCheckOrder _order;

    /// <summary>Whether the answer is worth reading - there is none until a warehouse is chosen.</summary>
    [ObservableProperty]
    private string _summary = string.Empty;

    public bool HasSummary => Summary.Length > 0;

    /// <summary>Only when something is actually short; there is nothing to raise otherwise.</summary>
    [ObservableProperty]
    private bool _isShortOfSomething;

    [ObservableProperty]
    private string _message = string.Empty;

    public bool HasMessage => Message.Length > 0;

    /// <summary>
    /// Both questions this panel asks - can this list be done from the shelves, and what should be
    /// restocked - are worked out by the server against stock it holds. Neither can be answered here.
    /// </summary>
    public ConnectionRequirement Connection { get; }

    /// <summary>Raised when the panel has changed something the screen has to re-read - see GenerateInventory.</summary>
    public event EventHandler? Changed;

    public async Task ShowAsync(LocalTaskList taskList, CancellationToken cancellationToken = default)
    {
        IsOffered = taskList.IsGroup;
        _taskListServerId = taskList.ServerId;
        _taskListLocalId = taskList.LocalId;
        Message = string.Empty;

        // Read before anything is drawn, and assigned without saving it back - this is what was already
        // chosen, not somebody choosing it again.
        var reading = _reading.Read(_taskListLocalId);
        _isShowingWhatIsStored = true;
        IsFolded = reading.IsStockCheckFolded;
        Order = reading.StockOrder;
        _isShowingWhatIsStored = false;

        if (!IsOffered || _taskListServerId is null)
        {
            return;
        }

        await ShowWarehousesAsync(taskList.LinkedWarehouseId, cancellationToken);
        await AskAsync(cancellationToken);
    }

    /// <summary>
    /// Builds a shelf from the work and points the list at it. The screen re-reads afterwards, because
    /// the list now has a warehouse it did not have.
    /// </summary>
    [RelayCommand]
    private async Task GenerateInventoryAsync(CancellationToken cancellationToken)
    {
        if (_taskListServerId is not { } serverId)
        {
            return;
        }

        try
        {
            Message = await _tasks.GenerateInventoryAsync(serverId, cancellationToken) is not null
                ? _translations["Built a warehouse from what this list needs."]
                : _translations["There was nothing on this list to build a warehouse from."];

            Changed?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            Message = _translations["Couldn't reach Orbit just now."];
        }
    }

    /// <summary>
    /// Rebuilds the restock list against the warehouse behind it and the settings that warehouse
    /// carries - what somebody presses when the world changed rather than the list - and then reads the
    /// check again, since the point of asking is to see the answer.
    ///
    /// This is what Orbit.Web's rebuild put in place of two half-actions, and the phone was still
    /// offering one of them: "recalculate against the inventory" reconciled the list one way and left
    /// the reader to work out the rest.
    /// </summary>
    [RelayCommand]
    private async Task RefreshFromTheWarehouseAsync(CancellationToken cancellationToken)
    {
        if (LinkedWarehouse?.ServerId is not { } warehouseId)
        {
            return;
        }

        try
        {
            var refreshed = await _inventory.RefreshRestockListAsync(warehouseId, cancellationToken);
            Message = refreshed is { AddedCount: 0, RemovedCount: 0 }
                ? _translations["The restock list already asks for exactly what it should."]
                : _translations.Format(
                    "Restock list updated: {0} added, {1} removed.", refreshed.AddedCount, refreshed.RemovedCount);
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            Message = _translations["Couldn't reach Orbit just now."];
            return;
        }

        await AskAsync(cancellationToken);
    }

    [RelayCommand]
    private async Task RaiseShortfallsAsync(CancellationToken cancellationToken)
    {
        if (_taskListServerId is not { } serverId)
        {
            return;
        }

        try
        {
            var added = await _tasks.RaiseStockShortfallsAsync(serverId, cancellationToken);
            Message = added > 0
                ? _translations.Format("Added {0} to the restock list.", added)
                : _translations["Nothing new to add - what is short is already waiting there."];
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            Message = _translations["Couldn't reach Orbit just now."];
        }
    }

    private async Task ShowWarehousesAsync(Guid? linkedWarehouseId, CancellationToken cancellationToken)
    {
        var stored = await _warehouses.GetAllAsync(cancellationToken);

        Warehouses.Clear();
        Warehouses.Add(new WarehouseChoice(null, _translations["Not measured against a warehouse"]));
        foreach (var warehouse in stored.Where(warehouse => warehouse.ServerId is not null))
        {
            Warehouses.Add(new WarehouseChoice(warehouse.ServerId, warehouse.Name));
        }

        // Assigned without going back to the server: this is what the list already says it points at.
        _isShowingWhatIsStored = true;
        LinkedWarehouse = Warehouses.FirstOrDefault(choice => choice.ServerId == linkedWarehouseId)
            ?? Warehouses[0];
        _isShowingWhatIsStored = false;
    }

    /// <summary>True while the panel is filling itself in, so choosing does not look like a person choosing.</summary>
    private bool _isShowingWhatIsStored;

    private async Task AskAsync(CancellationToken cancellationToken)
    {
        Requirements.Clear();
        _asCounted.Clear();
        IsShortOfSomething = false;

        if (_taskListServerId is not { } serverId || LinkedWarehouse?.ServerId is null)
        {
            Summary = string.Empty;
            return;
        }

        try
        {
            if (await _tasks.GetStockCheckAsync(serverId, cancellationToken) is not { } check)
            {
                Summary = string.Empty;
                return;
            }

            _asCounted.Clear();
            _asCounted.AddRange(check.Requirements.Select(StockRequirementRow.From));
            ShowInChosenOrder();

            IsShortOfSomething = !check.IsAchievable;
            Summary = check.IsAchievable
                ? _translations["Everything this list needs is on the shelf."]
                : _translations.Format(
                    "{0} of what this needs is short.",
                    check.Requirements.Count(requirement => requirement.Missing > 0));
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            // The arithmetic is the server's, and a stale answer about a shelf is worse than none.
            Summary = _translations["Couldn't work out what this needs without a connection."];
        }
    }

    async partial void OnLinkedWarehouseChanged(WarehouseChoice? value)
    {
        if (_isShowingWhatIsStored || _taskListServerId is not { } serverId)
        {
            return;
        }

        try
        {
            await _tasks.LinkWarehouseAsync(serverId, value?.ServerId, CancellationToken.None);
            await AskAsync(CancellationToken.None);
            Changed?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            Message = _translations["Couldn't reach Orbit just now."];
        }
    }

    /// <summary>Puts the panel away, or brings it back - what its heading and its chevron both do.</summary>
    [RelayCommand]
    private void ToggleFold() => IsFolded = !IsFolded;

    /// <summary>
    /// Reads the rows in one of the four orders, without asking the server again: the answer has not
    /// changed, only how it is being read. Sorted from the order it was counted in rather than from what
    /// is on screen, so switching back and forth cannot compound.
    /// </summary>
    private void ShowInChosenOrder()
    {
        Requirements.Clear();
        foreach (var row in InChosenOrder())
        {
            Requirements.Add(row);
        }
    }

    private IEnumerable<StockRequirementRow> InChosenOrder() => Order switch
    {
        StockCheckOrder.Alphabetical => _asCounted.OrderBy(row => row.Name, StringComparer.CurrentCultureIgnoreCase),
        StockCheckOrder.ReverseAlphabetical => _asCounted.OrderByDescending(row => row.Name, StringComparer.CurrentCultureIgnoreCase),
        // Shortfalls first, and within them the order they were counted in - a second key by name would
        // hide which of them the work asks for first.
        StockCheckOrder.ShortFirst => _asCounted.OrderByDescending(row => row.IsShort),
        _ => _asCounted
    };

    partial void OnIsFoldedChanged(bool value)
    {
        OnPropertyChanged(nameof(IsNotFolded));
        OnPropertyChanged(nameof(FoldGlyph));
        OnPropertyChanged(nameof(FoldDescription));
        Remember();
    }

    partial void OnOrderChanged(StockCheckOrder value)
    {
        ShowInChosenOrder();
        Remember();
    }

    private void Remember()
    {
        if (_isShowingWhatIsStored || _taskListLocalId == Guid.Empty)
        {
            return;
        }

        _reading.Write(_taskListLocalId, new ChecklistReading(IsFolded, Order));
    }

    partial void OnSummaryChanged(string value) => OnPropertyChanged(nameof(HasSummary));

    partial void OnMessageChanged(string value) => OnPropertyChanged(nameof(HasMessage));
}
