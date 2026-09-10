using Orbit.Mobile.Localization;
using System.ComponentModel;
using System.Windows.Input;
using Orbit.Maui.Controls;
using Orbit.Mobile.Screens;
using Orbit.Mobile.Screens.Tasks;

namespace Orbit.Maui.Features.Tasks;

public partial class TaskListDetailPage : ContentPage, ITitleMenu, ITitleSteps
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
		ShowTitleMenuCommand = new Command(ShowListMenu);
		ChooseInventoryCommand = new Command(() => _ = ChooseInventoryAsync());
		ChooseStockOrderCommand = new Command(() => _ = ChooseStockOrderAsync());

		InitializeComponent();
		BindingContext = viewModel;
		AddButton.Command = NewItemForm.Toggling(AddRow, AddField);
	}

	/// <summary>
	/// The two arrows beside the screen's name. They step between entries while one is open, and are
	/// off the bar entirely while the list is showing - there is nothing to step through then, and the
	/// bar hides an arrow whose command has nothing to do. See ITitleSteps.
	/// </summary>
	public ICommand PreviousCommand => _viewModel.EditPreviousItemCommand;

	/// <inheritdoc cref="PreviousCommand"/>
	public ICommand NextCommand => _viewModel.EditNextItemCommand;

	public string PreviousDescription => _translations["Previous"];

	public string NextDescription => _translations["Next"];

	/// <summary>
	/// What the rail's "⋯" opens: how to read the list, and what can be done to the list as a whole -
	/// one menu holding both, which is where Orbit.Web's checklist keeps them too.
	/// </summary>
	public ICommand ShowTitleMenuCommand { get; }

	/// <summary>The panel it draws - one per screen, above everything else on it.</summary>
	public ScreenMenu Menu { get; } = new();

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

	/// <summary>
	/// What order to read the entries in, what to do about the shelf behind the list, and what can be
	/// done to the list itself - three questions under three headings rather than one run of nine
	/// words, which is the design's own menu and the shape every other menu in the app took on
	/// 2026-09-09. "In list order" and "Delete list" sitting in the same column, one under the other,
	/// is two different kinds of press a thumb's width apart.
	///
	/// The order in force is marked, because a menu of three with no answer among them leaves the
	/// reader guessing what they are looking at, and the order stays open while somebody tries one and
	/// then another.
	/// </summary>
	private void ShowListMenu()
	{
		List<ScreenMenuEntry> shelf = [];
		List<ScreenMenuEntry> list = [];

		// The two that price a list against a shelf are only worth offering where there is a shelf to
		// price it against - the panel below appears by the same rule. Their own group: they are about
		// the inventory behind the list rather than about the list.
		if (_viewModel.StockCheck.IsOffered)
		{
			// And building one is only worth offering where there is something on the list a shelf
			// would be about - see GeneratedInventorySource, which the browser's own menu asks too.
			if (_viewModel.HasSomethingToBuildAStorageFrom)
			{
				// Asks what to build rather than building it - see GenerateInventoryForm, and the
				// browser's own overlay, which asks the same six things at the same moment.
				shelf.Add(new ScreenMenuEntry(
					_translations["Generate inventory"],
					() => _viewModel.StockCheck.AskWhatToBuildCommand.Execute(null)));
			}

			shelf.Add(new ScreenMenuEntry(
				_translations["Refresh the restock list"],
				() => _viewModel.StockCheck.RefreshFromTheInventoryCommand.Execute(null)));
		}

		// The list's own fields - its name, what it is, how much it matters - which stood at the top of
		// the page and are now behind this: the screen is the entries, and those fields are for the one
		// reader in a hundred who came to change the list rather than to tick something off it.
		if (_viewModel.CanEdit)
		{
			list.Add(new ScreenMenuEntry(
				_translations["Edit"],
				() => ListFields.IsVisible = ListSettings.IsVisible = !ListSettings.IsVisible,
				ListSettings.IsVisible));

			// Whether the list is finished - the box the browser's editor draws, with the same three
			// answers behind it: it ticks itself once every entry is, and pressing it says so about the
			// list rather than about its entries, so a list can sit in Finished with work still on it
			// and be not finished with none - see TaskListCompletion, and the view model's IsFinished.
			list.Add(new ScreenMenuEntry(
				_translations["Completed"],
				() => _viewModel.IsFinished = !_viewModel.IsFinished,
				_viewModel.IsFinished));
		}

		// The same, for offering it to somebody else. Absent for a private list, which has no readable
		// copy on the server to hand anybody - see SharePanel.CanShare.
		list.Add(new ScreenMenuEntry(
			_translations["Share"],
			() => Sharing.IsVisible = !Sharing.IsVisible,
			Sharing.IsVisible,
			canBeChosen: !_viewModel.IsPrivate));

		// What used to be a row of words under the last entry, which on a long list is nowhere near the
		// thumb. Deleting is offered only where this reader may change the list at all.
		if (_viewModel.CanEdit)
		{
			list.Add(new ScreenMenuEntry(
				_translations["Delete list"], () => _ = DeleteAsync()));
		}

		// Where this thing's own copies are found again - see CopyHistoryViewModel. Only once there is
		// one, and here rather than in the account's menu: a history belongs to the thing it is the
		// history of.
		if (_viewModel.HasHistory)
		{
			list.Add(new ScreenMenuEntry(
				_translations["History"], () => _viewModel.GoToHistoryCommand.Execute(null)));
		}

		// Where it is filed, which is a question about the list rather than about the work on it. Only
		// where this reader may change the list at all - filing is the owner's decision about their own
		// page. "No folder" is one of the answers rather than a way of undoing the others: a list in
		// none is in a built-in folder, which is not nothing - see FolderPlacement.
		List<ScreenMenuEntry> folders = [];

		if (_viewModel.CanEdit)
		{
			folders.Add(new ScreenMenuEntry(
				_translations["No folder"],
				() => _viewModel.FileCommand.Execute(null),
				_viewModel.FolderId is null));

			folders.AddRange(_viewModel.Folders.Select(folder => new ScreenMenuEntry(
				folder.Name,
				() => _viewModel.FileCommand.Execute(folder.LocalId),
				folder.LocalId == _viewModel.FolderId)));
		}

		// An empty group is left out rather than drawn as a heading over nothing, which is what lets the
		// shelf's two and the folders be offered only sometimes - see ScreenMenu.ShowGroups.
		Menu.ShowGroups(
		[
			new ScreenMenuGroup(_translations["Sort"],
			[
				Order(_translations["In list order"], ChecklistOrder.AsArranged),
				Order(_translations["A to Z"], ChecklistOrder.Alphabetical),
				Order(_translations["Left to do first"], ChecklistOrder.UndoneFirst)
			]),
			new ScreenMenuGroup(_translations["Folder"], folders),
			new ScreenMenuGroup(_translations["Inventory"], shelf),
			new ScreenMenuGroup(_translations["List"], list)
		]);
	}

	/// <summary>Asked first, as every delete in Orbit is - and named, so the question says which list.</summary>
	private async Task DeleteAsync()
	{
		var question = _translations.Format("Delete task list \"{0}\"?", _viewModel.Title);
		if (await Confirmation.AskAsync(this, question, _translations["Delete"], _translations["Cancel"]))
		{
			_viewModel.DeleteListCommand.Execute(null);
		}
	}

	private ScreenMenuEntry Order(string name, ChecklistOrder order) => new(
		name,
		() =>
		{
			_viewModel.ItemOrder = order;

			// Asked again rather than ticked here: the tick has to leave whichever entry was carrying
			// it, and only the choices themselves know which that was.
			ShowListMenu();
		},
		_viewModel.ItemOrder == order,
		staysOpen: true);

	private string MarkStock(string name, StockCheckOrder order)
		=> _viewModel.StockCheck.Order == order ? $"{name} ✓" : name;

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
	/// The same two rules as the link picker above, for the same two reasons: the choice is let go of
	/// before the step is added, and both happen after the picker's own selection has finished. See
	/// OnLinkedTaskListPicked, which explains what each of them is avoiding.
	/// </summary>
	private void OnStepPicked(object? sender, EventArgs eventArgs)
	{
		if (sender is not Picker picker || picker.SelectedItem is not TaskEntryChoice chosen)
		{
			return;
		}

		Dispatcher.Dispatch(() =>
		{
			picker.SelectedIndex = -1;
			_viewModel.BeingEdited?.WaitForCommand.Execute(chosen);
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
