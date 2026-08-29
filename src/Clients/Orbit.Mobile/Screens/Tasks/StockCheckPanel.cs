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
    private readonly LocalWarehouseRepository _warehouses;
    private readonly Translations _translations;

    private Guid? _taskListServerId;

    public StockCheckPanel(TasksClient tasks, LocalWarehouseRepository warehouses, Translations translations)
    {
        _tasks = tasks;
        _warehouses = warehouses;
        _translations = translations;
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

    /// <summary>Raised when the panel has changed something the screen has to re-read - see GenerateInventory.</summary>
    public event EventHandler? Changed;

    public async Task ShowAsync(LocalTaskList taskList, CancellationToken cancellationToken = default)
    {
        IsOffered = taskList.IsGroup;
        _taskListServerId = taskList.ServerId;
        Message = string.Empty;

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

    [RelayCommand]
    private Task RecalculateAsync(CancellationToken cancellationToken) => AskAsync(cancellationToken);

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

            foreach (var requirement in check.Requirements)
            {
                Requirements.Add(StockRequirementRow.From(requirement));
            }

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

    partial void OnSummaryChanged(string value) => OnPropertyChanged(nameof(HasSummary));

    partial void OnMessageChanged(string value) => OnPropertyChanged(nameof(HasMessage));
}
