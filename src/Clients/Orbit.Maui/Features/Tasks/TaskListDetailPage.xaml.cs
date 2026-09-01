using Orbit.Mobile.Localization;
using System.ComponentModel;
using System.Windows.Input;
using Orbit.Mobile.Screens.Tasks;

namespace Orbit.Maui.Features.Tasks;

public partial class TaskListDetailPage : ContentPage
{
	/// <summary>
	/// Typed so the item template's bindings back up to the page can be compiled - see the comment in
	/// the XAML about why they go through the page rather than naming the view model directly.
	/// </summary>
	public TaskListDetailViewModel ViewModel => _viewModel;

	private readonly TaskListDetailViewModel _viewModel;
	private readonly Translations _translations;

	public TaskListDetailPage(TaskListDetailViewModel viewModel, Translations translations)
	{
		// Before InitializeComponent: the list's own menu is bound from the static part of the tree,
		// which is built there and reads the property once. A command assigned afterwards is read as
		// null and never looked at again, and the button then does nothing.
		_translations = translations;
		_viewModel = viewModel;
		ShowItemMenuCommand = new Command<TaskItemRow>(item => _ = ShowItemMenuAsync(item));
		ShowListMenuCommand = new Command(() => _ = ShowListMenuAsync());
		ChooseWarehouseCommand = new Command(() => _ = ChooseWarehouseAsync());

		InitializeComponent();
		BindingContext = viewModel;
	}

	/// <summary>
	/// What the list's "⋯" opens: the two things Orbit.Web keeps in its overflow menu, which are about
	/// the list as a whole rather than about any one entry.
	/// </summary>
	public ICommand ShowListMenuCommand { get; }

	/// <summary>Which shelf this list's work is measured against - see StockCheckPanel.</summary>
	public ICommand ChooseWarehouseCommand { get; }

	private async Task ChooseWarehouseAsync()
	{
		var names = _viewModel.StockCheck.Warehouses.Select(warehouse => warehouse.Name).ToArray();
		var chosen = await DisplayActionSheet(
			_translations["Can this be done?"], _translations["Cancel"], destruction: null, names);

		if (_viewModel.StockCheck.Warehouses.FirstOrDefault(warehouse => warehouse.Name == chosen) is { } picked)
		{
			_viewModel.StockCheck.LinkedWarehouse = picked;
		}
	}

	private async Task ShowListMenuAsync()
	{
		var generate = _translations["Generate inventory"];
		var refresh = _translations["Refresh the restock list"];
		var chosen = await DisplayActionSheet(
			_translations["List options"], _translations["Cancel"], destruction: null, generate, refresh);

		if (chosen == generate)
		{
			_viewModel.StockCheck.GenerateInventoryCommand.Execute(null);
		}
		else if (chosen == refresh)
		{
			_viewModel.StockCheck.RefreshFromTheWarehouseCommand.Execute(null);
		}
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_viewModel.PropertyChanged += OnViewModelPropertyChanged;
		_viewModel.LoadCommand.Execute(null);
	}

	/// <summary>
	/// Choosing a list moves the entry, which closes the editor the picker lives in - and iOS leaves its
	/// wheel on screen when the view under it disappears. Dismissing it first is the view's own business.
	/// </summary>
	private void OnMoveTargetChosen(object? sender, EventArgs eventArgs)
	{
		(sender as Picker)?.Unfocus();
	}

	/// <summary>Lets go of the edit lock as the screen leaves - see EditLock.</summary>
	protected override async void OnDisappearing()
	{
		base.OnDisappearing();
		_viewModel.PropertyChanged -= OnViewModelPropertyChanged;
		await _viewModel.CloseAsync();
	}

	/// <summary>
	/// What a row's "⋯" opens. On the page rather than the view model because an action sheet is a
	/// page's own presentation - the same reason ConversationPage keeps its message menu here.
	/// </summary>
	public ICommand ShowItemMenuCommand { get; }

	/// <summary>
	/// The question the view model arms when "Update stock levels" is crossed off with errands still
	/// open - see TaskListDetailViewModel.RestockTickBeingAsked. Asked from the page rather than there
	/// because a confirmation prompt is the platform's, the same split as the action sheets above.
	/// </summary>
	private async void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
	{
		if (eventArgs.PropertyName != nameof(TaskListDetailViewModel.IsAskingToFinishRestocking)
			|| !_viewModel.IsAskingToFinishRestocking)
		{
			return;
		}

		var finishesTheRound = await DisplayAlertAsync(
			_translations["Update stock levels"],
			_translations["Finish this list and set every item in the warehouse to its minimum?"],
			_translations["Finish the whole list"],
			_translations["Just this one"]);

		await (finishesTheRound
			? _viewModel.FinishRestockingCommand.ExecuteAsync(null)
			: _viewModel.TickOnlyThisCommand.ExecuteAsync(null));
	}

	private async Task ShowItemMenuAsync(TaskItemRow? item)
	{
		if (item is null)
		{
			return;
		}

		var remove = _translations["Delete item"];
		var moveUp = _translations["Move up"];
		var moveDown = _translations["Move down"];
		var chosen = await DisplayActionSheet(
			_translations["Item options"], _translations["Cancel"], remove,
			_translations["Edit"], moveUp, moveDown);

		if (chosen == remove)
		{
			_viewModel.RemoveItemCommand.Execute(item);
		}
		else if (chosen == _translations["Edit"])
		{
			_viewModel.EditItemCommand.Execute(item);
		}
		else if (chosen == moveUp)
		{
			_viewModel.MoveItemUpCommand.Execute(item);
		}
		else if (chosen == moveDown)
		{
			_viewModel.MoveItemDownCommand.Execute(item);
		}
	}
}
