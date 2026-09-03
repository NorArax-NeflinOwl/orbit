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
		ChooseInventoryCommand = new Command(() => _ = ChooseInventoryAsync());
		ChooseStockOrderCommand = new Command(() => _ = ChooseStockOrderAsync());

		InitializeComponent();
		BindingContext = viewModel;
	}

	/// <summary>
	/// What the list's "⋯" opens: the two things Orbit.Web keeps in its overflow menu, which are about
	/// the list as a whole rather than about any one entry.
	/// </summary>
	public ICommand ShowListMenuCommand { get; }

	/// <summary>Which shelf this list's work is measured against - see StockCheckPanel.</summary>
	public ICommand ChooseInventoryCommand { get; }

	/// <summary>What order that panel lists what the work needs in - the same four Orbit.Web offers.</summary>
	public ICommand ChooseStockOrderCommand { get; }

	private async Task ChooseStockOrderAsync()
	{
		var names = new Dictionary<string, StockCheckOrder>
		{
			[MarkStock(_translations["In list order"], StockCheckOrder.AsCounted)] = StockCheckOrder.AsCounted,
			[MarkStock(_translations["A to Z"], StockCheckOrder.Alphabetical)] = StockCheckOrder.Alphabetical,
			[MarkStock(_translations["Z to A"], StockCheckOrder.ReverseAlphabetical)] = StockCheckOrder.ReverseAlphabetical,
			[MarkStock(_translations["Short first"], StockCheckOrder.ShortFirst)] = StockCheckOrder.ShortFirst
		};

		var chosen = await DisplayActionSheet(
			_translations["Sort"], _translations["Cancel"], destruction: null, [.. names.Keys]);

		if (chosen is not null && names.TryGetValue(chosen, out var order))
		{
			_viewModel.StockCheck.Order = order;
		}
	}

	private async Task ChooseInventoryAsync()
	{
		var names = _viewModel.StockCheck.Inventories.Select(inventory => inventory.Name).ToArray();
		var chosen = await DisplayActionSheet(
			_translations["Can this be done?"], _translations["Cancel"], destruction: null, names);

		if (_viewModel.StockCheck.Inventories.FirstOrDefault(inventory => inventory.Name == chosen) is { } picked)
		{
			_viewModel.StockCheck.LinkedInventory = picked;
		}
	}

	private async Task ShowListMenuAsync()
	{
		// What order to read the entries in, then what to do to the list - one menu holding both, as
		// Orbit.Web's checklist keeps them. The order in force is marked, because a menu of three with
		// no answer among them leaves the reader guessing what they are looking at.
		var orders = new Dictionary<string, ChecklistOrder>
		{
			[Mark(_translations["In list order"], ChecklistOrder.AsArranged)] = ChecklistOrder.AsArranged,
			[Mark(_translations["A to Z"], ChecklistOrder.Alphabetical)] = ChecklistOrder.Alphabetical,
			[Mark(_translations["Left to do first"], ChecklistOrder.UndoneFirst)] = ChecklistOrder.UndoneFirst
		};

		// The two that price a list against a shelf are only worth offering where there is a shelf to
		// price it against - the panel below appears by the same rule.
		var generate = _translations["Generate inventory"];
		var refresh = _translations["Refresh the restock list"];
		string[] choices = _viewModel.StockCheck.IsOffered
			? [.. orders.Keys, generate, refresh]
			: [.. orders.Keys];

		var chosen = await DisplayActionSheet(
			_translations["List options"], _translations["Cancel"], destruction: null, choices);

		if (chosen is not null && orders.TryGetValue(chosen, out var order))
		{
			_viewModel.ItemOrder = order;
		}
		else if (chosen == generate)
		{
			_viewModel.StockCheck.GenerateInventoryCommand.Execute(null);
		}
		else if (chosen == refresh)
		{
			_viewModel.StockCheck.RefreshFromTheInventoryCommand.Execute(null);
		}
	}

	private string MarkStock(string name, StockCheckOrder order)
		=> _viewModel.StockCheck.Order == order ? $"{name} ✓" : name;

	private string Mark(string name, ChecklistOrder order)
		=> _viewModel.ItemOrder == order ? $"{name} ✓" : name;

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_viewModel.PropertyChanged += OnViewModelPropertyChanged;
		_viewModel.LoadCommand.Execute(null);
	}

	/// <summary>
	/// Takes what the picker was pointed at and lets go of it again, so it goes on saying what to add
	/// next rather than what the entry already stands for.
	///
	/// Both happen after the picker's own selection has finished rather than during it: adding a list
	/// changes what the picker offers, and changing a picker's source or its selection from inside its
	/// own change hung the app on Android - the dialog stopped answering and the screen was reported as
	/// not responding.
	///
	/// The selection is let go of <em>before</em> the link is added, the order the move picker beside
	/// this one already uses. Adding a link takes the chosen list out of what the picker offers, and a
	/// selection still pointing past the end of the shortened list is pulled back onto whatever now sits
	/// last - which raises this handler a second time and linked a list nobody had chosen. Choosing the
	/// last list on offer was enough to link the one above it as well, and the entry then waited on both.
	/// With nothing selected there is no index for the shortening to move.
	/// </summary>
	private void OnLinkedTaskListPicked(object? sender, EventArgs eventArgs)
	{
		if (sender is not Picker picker || picker.SelectedItem is not TaskListChoice chosen)
		{
			return;
		}

		Dispatcher.Dispatch(() =>
		{
			picker.SelectedIndex = -1;
			_viewModel.BeingEdited?.LinkToCommand.Execute(chosen);
		});
	}

	/// <summary>
	/// Choosing a list moves the entry, which closes the editor the picker lives in - and iOS leaves its
	/// wheel on screen when the view under it disappears. Dismissing it first is the view's own business.
	///
	/// The move itself happens after the picker's own selection has finished, for the reason the link
	/// picker beside it does the same: closing the editor and rebuilding what the picker offers from
	/// inside its own change is what hung the app on Android.
	/// </summary>
	private void OnMoveTargetChosen(object? sender, EventArgs eventArgs)
	{
		if (sender is not Picker picker || picker.SelectedItem is not TaskListChoice chosen)
		{
			return;
		}

		picker.Unfocus();
		Dispatcher.Dispatch(() =>
		{
			picker.SelectedIndex = -1;
			_viewModel.MoveItemCommand.Execute(chosen);
		});
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
		// The entry the last add put on the list, brought into view. The checklist shares this screen
		// with everything the list itself carries, so it is often drawn shorter than it is - and a new
		// row below the fold, with a cleared box above it, looks exactly like a tap that never landed.
		// The shelf screen brings a pointed-at row into view the same way.
		if (eventArgs.PropertyName == nameof(TaskListDetailViewModel.RowJustAdded)
			&& _viewModel.RowJustAdded is { } added)
		{
			Checklist.ScrollTo(added, position: ScrollToPosition.End, animate: true);
			return;
		}

		if (eventArgs.PropertyName == nameof(TaskListDetailViewModel.IsAskingAboutTheListsBehind)
			&& _viewModel.IsAskingAboutTheListsBehind)
		{
			await AskAboutTheListsBehindAsync();
			return;
		}

		if (eventArgs.PropertyName != nameof(TaskListDetailViewModel.IsAskingToFinishRestocking)
			|| !_viewModel.IsAskingToFinishRestocking)
		{
			return;
		}

		var finishesTheRound = await DisplayAlertAsync(
			_translations["Update stock levels"],
			_translations["Finish this list and set every item in the inventory to its minimum?"],
			_translations["Finish the whole list"],
			_translations["Just this one"]);

		await (finishesTheRound
			? _viewModel.FinishRestockingCommand.ExecuteAsync(null)
			: _viewModel.TickOnlyThisCommand.ExecuteAsync(null));
	}

	/// <summary>
	/// The question raised by pressing the box of an entry that stands for other lists - see
	/// TaskListDetailViewModel.AskAboutTheListsBehind. One list is a yes-or-no; several are a sheet, so
	/// the reader picks which of them they meant rather than being sent to the first.
	/// </summary>
	private async Task AskAboutTheListsBehindAsync()
	{
		var lists = _viewModel.ListsBehindTheEntry.ToList();
		if (lists.Count == 0)
		{
			// Nothing this phone can open: the question is still answered - what the entry is waiting on
			// is worth knowing - but there is no door to offer.
			await DisplayAlertAsync(
				_viewModel.LinkedTickBeingAsked?.Description ?? string.Empty,
				_viewModel.ListsBehindTheEntryQuestion,
				_translations["Close"]);
			_viewModel.LeaveTheListBehindCommand.Execute(null);
			return;
		}

		if (lists.Count == 1)
		{
			var opens = await DisplayAlertAsync(
				_viewModel.LinkedTickBeingAsked?.Description ?? string.Empty,
				_viewModel.ListsBehindTheEntryQuestion,
				_translations["Yes"],
				_translations["No"]);

			_viewModel.OpenTheListBehindCommand.Execute(opens ? lists[0] : null);
			return;
		}

		var chosen = await DisplayActionSheet(
			_viewModel.ListsBehindTheEntryQuestion, _translations["No"], null,
			[.. lists.Select(list => list.Label)]);

		_viewModel.OpenTheListBehindCommand.Execute(
			lists.FirstOrDefault(list => list.Label == chosen));
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
		// Moving an entry is only offered while the list is being read in the order it was arranged in:
		// anywhere else "up" would move it in an arrangement nobody can see, and the entry would stay
		// exactly where it is on screen.
		string[] choices = _viewModel.CanBeRearranged
			? [_translations["Edit"], moveUp, moveDown]
			: [_translations["Edit"]];
		var chosen = await DisplayActionSheet(
			_translations["Item options"], _translations["Cancel"], remove, choices);

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
