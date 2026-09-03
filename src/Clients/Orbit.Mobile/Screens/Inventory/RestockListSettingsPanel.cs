using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Contracts.Inventory;
using Orbit.Core.Inventory;
using Orbit.Mobile.Api;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens;
using Orbit.Mobile.Sync;

namespace Orbit.Mobile.Screens.Inventory;

/// <summary>
/// How a warehouse's restock list is built, and when it comes round - the settings Orbit.Web has had at
/// the bottom of its warehouse editor and the phone had no way to see, let alone change. A shelf could
/// be edited here while the rule that decides what it asks for was reachable only from a browser.
///
/// Its own object rather than five more fields on the warehouse screen: two settings, two actions and a
/// message that only ever concern each other - and the screen it sits on is about the shelf, not about
/// the list the shelf feeds.
///
/// Needs a connection, and says so rather than offering buttons that can only fail: the settings live on
/// the server and nothing local stands in for them.
/// </summary>
public sealed partial class RestockListSettingsPanel : ObservableObject
{
    private readonly InventoryClient _inventory;
    private readonly Translations _translations;

    private Guid? _warehouseServerId;

    public RestockListSettingsPanel(
        InventoryClient inventory, Translations translations, ConnectionRequirement connection)
    {
        _inventory = inventory;
        _translations = translations;
        Connection = connection;
    }

    /// <summary>Whether there is a connection - see <see cref="ConnectionRequirement"/>.</summary>
    public ConnectionRequirement Connection { get; }

    /// <summary>
    /// Only what some dated task is waiting on, rather than everything below its own minimum. The
    /// narrower rule is what somebody wants when a shelf holds things nobody is asking for yet.
    /// </summary>
    [ObservableProperty]
    private bool _onlyLinkedWithDueDate;

    /// <summary>When the standing "Update stock levels" reminder arrives.</summary>
    [ObservableProperty]
    private TimeSpan _refreshTime = RestockListSettings.DefaultRefreshTimeOfDay.ToTimeSpan();

    [ObservableProperty]
    private string _message = string.Empty;

    /// <summary>
    /// Whether to show the panel at all. A warehouse the server has never seen has no list to build,
    /// and one shared read-only has settings that are not this reader's to change.
    /// </summary>
    [ObservableProperty]
    private bool _isOffered;

    public bool HasMessage => Message.Length > 0;

    /// <summary>What the checkbox above means right now, said in words rather than left to be guessed.</summary>
    public string RuleDescription
        => _translations[OnlyLinkedWithDueDate
            ? "The list asks for products some task with a due date needs. What is running low but nothing is waiting on is left off."
            : "The list asks for everything on this shelf that has dropped below its own minimum."];

    /// <summary>Which warehouse this is about, by the name the server knows it by - null while it has none.</summary>
    public async Task ShowFor(Guid? warehouseServerId, CancellationToken cancellationToken)
    {
        _warehouseServerId = warehouseServerId;
        Message = string.Empty;

        if (warehouseServerId is not { } serverId)
        {
            IsOffered = false;
            return;
        }

        try
        {
            if (await _inventory.GetRestockListSettingsAsync(serverId, cancellationToken) is not { } settings)
            {
                IsOffered = false;
                return;
            }

            OnlyLinkedWithDueDate = settings.OnlyLinkedWithDueDate;
            RefreshTime = settings.RefreshTimeOfDay.ToTimeSpan();
            IsOffered = true;
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            // Nothing local stands in for these, so with no connection the panel simply is not there -
            // which is honest, and better than showing the defaults as though they were the settings.
            IsOffered = false;
        }
    }

    /// <summary>Saves the settings and rebuilds the list to match, saying what that moved.</summary>
    [RelayCommand]
    private Task SaveAsync(CancellationToken cancellationToken)
        => ReportAsync(
            serverId => _inventory.SaveRestockListSettingsAsync(
                serverId,
                new RestockListSettingsDto(OnlyLinkedWithDueDate, TimeOnly.FromTimeSpan(RefreshTime)),
                cancellationToken),
            cancellationToken);

    /// <summary>Rebuilds the list against the settings it already has, without changing them.</summary>
    [RelayCommand]
    private Task RefreshAsync(CancellationToken cancellationToken)
        => ReportAsync(
            serverId => _inventory.RefreshRestockListAsync(serverId, cancellationToken), cancellationToken);

    private async Task ReportAsync(
        Func<Guid, Task<RestockRefreshResultDto>> act, CancellationToken cancellationToken)
    {
        if (_warehouseServerId is not { } serverId)
        {
            return;
        }

        try
        {
            var moved = await act(serverId);
            Message = _translations.Format("Added {0}, removed {1}.", moved.AddedCount, moved.RemovedCount);
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            Message = _translations["The restock list needs a connection."];
        }
    }

    partial void OnMessageChanged(string value) => OnPropertyChanged(nameof(HasMessage));

    partial void OnOnlyLinkedWithDueDateChanged(bool value) => OnPropertyChanged(nameof(RuleDescription));
}
